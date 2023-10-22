using ValveResourceFormat;
using SteamDatabase.ValvePak;
using System.IO.Enumeration;

if (args.Length == 0)
{
    Console.WriteLine("Please specify a '.vpk' map file or folder.");
    Console.ReadKey(true);
    return;
}

if (Directory.Exists(args[0]))
{
    PackVpkFromDirectory(args[0]);
    return;
}
else if (!Path.GetExtension(args[0]).Contains(".vpk"))
{
    Console.WriteLine("Invalid file specified.");
    Console.ReadKey(true);
    return;
}

var vpkFile = args[0];

var mod = Directory.GetParent(vpkFile);
var game = null as DirectoryInfo;
while (mod != null && mod.Name != "game")
{
    if (mod.Name == "maps")
    {
        mod = mod.Parent;
        game = mod;
        while (game != null && game.Name != "game")
        {
            game = game.Parent;
        }
        break;
    }

    mod = mod.Parent;
}

if (mod is null || game is null)
{
    Console.WriteLine("Map file is not inside the 'game/<mod_or_addon>/maps' folder. Please drag it from within that folder.");
    Console.ReadKey();
    return;
}

Console.WriteLine("Do you want to attempt to auto-pack the map vpk? (y/n)\n" +
    "You can skip this if you want to add extra files like sounds manually.\n");
ConsoleKeyInfo key = Console.ReadKey(true);

bool doVpkPack = key.KeyChar == 'y';

// Backup the original vpk
if (doVpkPack)
{
    File.Copy(vpkFile, vpkFile + "_unpacked", true);
}

var mapname = Path.GetFileNameWithoutExtension(vpkFile);

// Create a brand new folder to extract the map into.
// Since source2 doesn't like loose map files inside the maps folder, lets do this outside of the addon/mod
var mapPackFolder = Path.Combine(game.FullName, "csgo_addons", "mappacker", $"{mapname}")!;
if (Directory.Exists(mapPackFolder))
{
    Directory.Delete(mapPackFolder, true);
}

var outPackage = new Package();
outPackage.Read(vpkFile);

var inPackageSet = new HashSet<string>();

ExtractPackage(outPackage, false);

if (!doVpkPack)
    outPackage.Dispose();

var vmap_path = Path.Combine(mapPackFolder, "maps", $"{mapname}.vmap_c");
var vmap_c = new Resource();
vmap_c.Read(vmap_path);
var new_files_added = 0;
CopyExternalReferences(vmap_c);

vmap_c.Dispose();

// Radar
AddExtraFile($"resource/overviews/{mapname}.txt");
AddExtraFile($"panorama/images/overheadmaps/{mapname}_radar_tga.vtex_c");

// Loading screen images
for (int i = 1; i <= 10; i++)
    AddExtraFile($"panorama/images/map_icons/screenshots/1080p/{mapname}_{i}_png.vtex_c");

if (!doVpkPack)
{
    // Can't repack the map, so just move the map folder next to the original vpk
    Console.WriteLine($"\nDone! Isolated all found assets into game/csgo_addons/mappacker/{mapname}\n" +
                       "You may drag that folder onto this tool to pack.");
}
else
{
    outPackage.Write(vpkFile + "_packed");

    // Release the .vpk before overwriting it
    outPackage.Dispose();
    File.Move(vpkFile + "_packed", vpkFile, true);

    Console.WriteLine($"\nDone! Packed {new_files_added} new files into {mapname}.");

    Directory.Delete(mapPackFolder, true);
}

Console.ReadKey();

void PackVpkFromDirectory(string dirPath)
{
    string vpkPath = dirPath + ".vpk";

    Console.WriteLine($"Packing {dirPath} into a vpk...");

    if (File.Exists(vpkPath))
        File.Delete(vpkPath);

    Package vpk = new Package();

    var mapFiles = new FileSystemEnumerable<string>(
        dirPath,
        (ref FileSystemEntry entry) => entry.ToSpecifiedFullPath(),
        new EnumerationOptions
        {
            RecurseSubdirectories = true,
        }
    );

    uint fileCount = 0;
    int vpkSize = 0;

    foreach (var file in mapFiles)
    {
        if (!File.Exists(file))
            continue;

        var name = file[(dirPath.Length + 1)..];
        var data = File.ReadAllBytes(file);
        vpk.AddFile(name, data);

        fileCount++;
        vpkSize += data.Length;
    }

    vpk.Write(vpkPath);
    vpk.Dispose();

    Console.WriteLine($"Wrote {Path.GetFileName(vpkPath)} with {fileCount} files totalling {vpkSize} bytes.");
    Console.ReadKey();
}

void ExtractPackage(Package package, bool vmapOnly)
{
    foreach (var entries in package.Entries.Values)
    {
        foreach (var entry in entries)
        {
            var filePath = entry.GetFullPath();
            inPackageSet.Add(filePath);

            if (vmapOnly && !filePath.Contains("vmap_c"))
                continue;

            var extractFilePath = Path.Combine(mapPackFolder, filePath);
            Directory.CreateDirectory(Path.GetDirectoryName(extractFilePath)!);

            package.ReadEntry(entry, out var data);
            File.WriteAllBytes(extractFilePath, data);
        }
    }

    Console.WriteLine($"Extracted {package.FileName}");
}

void AddExtraFile(string refFileName)
{
    var fullFilePath = Path.Combine(mod.FullName, refFileName);

    if (!File.Exists(fullFilePath))
    {
        Console.WriteLine($"Could not find '{refFileName}' in the mod folder.");
        return;
    }

    Console.WriteLine($"Adding '{refFileName}'");
    if (doVpkPack)
    {
        var data = File.ReadAllBytes(fullFilePath);
        outPackage.AddFile(refFileName, data);
    }
    else
    {
        var newFileFullPath = Path.Combine(mapPackFolder, refFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(newFileFullPath)!);
        File.Copy(fullFilePath, newFileFullPath, true);
    }
}

void AddExtraMap(string mapName)
{
    var mapPath = Path.Combine("maps", mapName + ".vpk");
    var mapFullPath = Path.Combine(mod.FullName, mapPath);

    if (!File.Exists(mapFullPath))
    {
        Console.WriteLine($"Could not find {mapPath} in the mod folder.");
        return;
    }

    Console.WriteLine($"Adding '{mapName}'");

    var mapPackage = new Package();
    mapPackage.Read(mapFullPath);

    ExtractPackage(mapPackage, true);
    mapPackage.Dispose();

    var vmap_path = Path.Combine(mapPackFolder, "maps", $"{mapName}.vmap_c");
    var vmap_c = new Resource();
    vmap_c.Read(vmap_path);
    CopyExternalReferences(vmap_c);

    vmap_c.Dispose();
    File.Delete(vmap_path);

    if (doVpkPack)
    {
        var data = File.ReadAllBytes(mapFullPath);
        outPackage.AddFile(mapPath, data);
    }
    else
    {
        var newMapFullPath = Path.Combine(mapPackFolder, "maps", mapName + ".vpk");
        File.Copy(mapFullPath, newMapFullPath, true);
    }

    new_files_added++;
}

void CopyExternalReferences(Resource resource, int depth = 0)
{
    if (resource.ExternalReferences is null)
        return;

    foreach (var resource_reference in resource.ExternalReferences.ResourceRefInfoList)
    {
        var refFileName = resource_reference.Name;

        // lua vscripts are authored directly in the game folder, they don't get compiled by the engine
        if (!refFileName.EndsWith(".lua"))
            refFileName += "_c";

        // Extra maps like skyboxes are handled separately, their vmap should not be packed
        if (refFileName.EndsWith(".vmap_c"))
        {
            AddExtraMap(Path.GetFileNameWithoutExtension(refFileName));
            continue;
        }

        if (refFileName.StartsWith('_') || inPackageSet.Contains(refFileName))
            continue;

        var fullRefFilePath = Path.Combine(mod.FullName, refFileName);
        if (!File.Exists(fullRefFilePath))
        {
            Console.WriteLine($"Could not find '{refFileName}' in the mod folder.");
            continue;
        }

        for (var i = 0; i < depth; i++)
            Console.Write("  ");

        Console.WriteLine($"Adding '{refFileName}'");

        using var child_resource = new Resource();

        if (!refFileName.EndsWith(".lua"))
        {
            try
            {
                child_resource.Read(fullRefFilePath);
            } catch { }
            CopyExternalReferences(child_resource, depth + 1);
        }

        // Copy the file to the map folder
        var newFullFilePath = Path.Combine(mapPackFolder, refFileName);

        if (File.Exists(newFullFilePath))
        {
            continue;
        }

        if (doVpkPack)
        {
            var data = File.ReadAllBytes(fullRefFilePath);
            outPackage.AddFile(refFileName, data);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(newFullFilePath)!);
        File.Copy(fullRefFilePath, newFullFilePath);

        new_files_added++;
    }
}
