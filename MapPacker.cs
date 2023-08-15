using ValveResourceFormat;
using SteamDatabase.ValvePak;
using System.Diagnostics;

if (args.Length == 0)
{
    Console.WriteLine("Please specify a '.vpk' map file.");
    Console.ReadKey();
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
    "You can skip this if you want to add extra files like sounds manually.");
ConsoleKeyInfo key = Console.ReadKey();

bool doVpkPack = key.KeyChar == 'y';

// This assumes the user has cs2 installed properly with csgo. Since cs2 doesn't have a vpk.exe, we use csgo's instead. It's sloppy but works...
var vpkExe = Path.Combine(game.Parent.FullName, "bin", "vpk.exe");

if (doVpkPack && !File.Exists(vpkExe))
{
    Console.WriteLine("Could not find 'vpk.exe' in your root bin folder, vpk will not be packed.\n" +
                      "Press any key to continue...");
    Console.ReadKey();

    doVpkPack = false;
}

// Backup the original vpk
if (doVpkPack)
{
    File.Copy(vpkFile, vpkFile + "_unpacked", true);
}

var mapname = Path.GetFileNameWithoutExtension(vpkFile);

// Create a brand new folder to extract the map into.
// Since source2 doesn't like loose map files inside the maps folder, let's do this outside of the addon/mod
var mapFolder = Path.Combine(game.FullName, "csgo_addons", "mappacker", $"{mapname}")!;
if (Directory.Exists(mapFolder))
{
    Directory.Delete(mapFolder, true);
}

var package = new Package();
package.Read(vpkFile);

var inPackage = new HashSet<string>();

ExtractPackage(package, false);

var vmap_path = Path.Combine(mapFolder, "maps", $"{mapname}.vmap_c");
var vmap_c = new Resource();
vmap_c.Read(vmap_path);
var new_files_added = 0;
CopyExternalReferences(vmap_c);

vmap_c.Dispose();
package.Dispose();

AddExtraFile($"/resource/overviews/{mapname}.txt");
AddExtraFile($"/panorama/images/overheadmaps/{mapname}_radar_tga.vtex_c");

if (!doVpkPack)
{
    // Can't repack the map, so just move the map folder next to the original vpk
    Console.WriteLine($"\nDone! Isolated all found assets into csgo_addons/mappacker/{mapname}\n" +
        "You may drag that folder into any vpk.exe to pack.");
}
else
{
    // Run vpk.exe to pack the map
    var vpkProcess = new Process();
    vpkProcess.StartInfo.FileName = vpkExe;
    vpkProcess.StartInfo.Arguments = $"\"{mapFolder}\"";
    vpkProcess.StartInfo.UseShellExecute = false;
    vpkProcess.StartInfo.CreateNoWindow = true;
    vpkProcess.Start();
    vpkProcess.WaitForExit();

    if (vpkProcess.ExitCode != 0)
    {
        Console.WriteLine("Failed to pack the map with 'vpk.exe'.");
        Console.ReadKey();
        return;
    }

    var finalMap = Path.Combine(Environment.CurrentDirectory, mapname + ".vpk");

    // Rename file
    File.Move(mapFolder + ".vpk", finalMap, true);
    Console.WriteLine($"\nDone! Packed {new_files_added} new files into {mapname}.");

    Directory.Delete(mapFolder, true);
}

Console.ReadKey();

void ExtractPackage(Package package, bool vmapOnly)
{
    foreach (var entries in package.Entries.Values)
    {
        foreach (var entry in entries)
        {
            var filePath = entry.GetFullPath();
            inPackage.Add(filePath);

            if (vmapOnly && !filePath.Contains("vmap_c"))
                continue;

            var extractFilePath = Path.Combine(mapFolder, filePath);
            Directory.CreateDirectory(Path.GetDirectoryName(extractFilePath)!);

            package.ReadEntry(entry, out var data);
            File.WriteAllBytes(extractFilePath, data);

            Console.WriteLine($"Extracted {filePath}");
        }
    }
}

void AddExtraFile(string refFileName)
{
    var fullFilePath = mod.FullName + refFileName;

    if (File.Exists(fullFilePath))
    {
        Console.WriteLine($"Found '{refFileName}' in the mod folder. Copying...");
        var newFileFullPath = mapFolder + refFileName;
        Directory.CreateDirectory(Path.GetDirectoryName(newFileFullPath)!);
        File.Copy(fullFilePath, newFileFullPath, true);
    }
    else
    {
        Console.WriteLine($"Could not find '{refFileName}' in the mod folder.");
    }
}

void AddExtraMap(string mapName)
{
    mapName.Remove(mapName.Length - 2, 2);
    var package = new Package();
    var mapPath = Path.Combine(mod.FullName, "maps", mapName + ".vpk");
    package.Read(mapPath);

    ExtractPackage(package, true);
    package.Dispose();

    var vmap_path = Path.Combine(mapFolder, "maps", $"{mapName}.vmap_c");
    var vmap_c = new Resource();
    vmap_c.Read(vmap_path);
    CopyExternalReferences(vmap_c);

    vmap_c.Dispose();
    File.Delete(vmap_path);

    var newMapFullPath = Path.Combine(mapFolder, "maps", mapName + ".vpk");
    File.Copy(mapPath, newMapFullPath, true);
    new_files_added++;
}

void CopyExternalReferences(Resource resource, int depth = 0)
{
    if (resource.ExternalReferences is null)
    {
        return;
    }

    foreach (var resource_reference in resource.ExternalReferences.ResourceRefInfoList)
    {
        var refFileName = resource_reference.Name;

        // lua vscripts are authored directly in the game folder, they don't get compiled by the engine
        if (!refFileName.EndsWith(".lua"))
        {
            refFileName += "_c";
        }

        // Extra maps like skyboxes are handled separately, their vmap should not be packed
        if (refFileName.EndsWith(".vmap_c"))
        {
            AddExtraMap(Path.GetFileNameWithoutExtension(refFileName));
            continue;
        }

        if (refFileName.StartsWith('_') || inPackage.Contains(refFileName))
        {
            continue;
        }

        var fullRefFilePath = Path.Combine(mod.FullName, refFileName);
        if (!File.Exists(fullRefFilePath))
        {
            Console.WriteLine($"Could not find '{refFileName}' in the mod folder.");
            continue;
        }

        for (var i = 0; i < depth; i++)
        {
            Console.Write("  ");
        }
        Console.WriteLine($"Found '{refFileName}' in the mod folder. Copying...");

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
        var newFullFilePath = Path.Combine(mapFolder, refFileName);

        if (File.Exists(newFullFilePath))
        {
            continue;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(newFullFilePath)!);
        File.Copy(fullRefFilePath, newFullFilePath);
        new_files_added++;
    }
}
