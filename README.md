# Asset Ripper MonoScripts GUID Fixer

UnityEditor tool that patches AssetRipper generated guids with the correct ones from the installed packages on your project.

This tool will try to fix incorrect GUID values in prefabs and scenes. Aditionally it will also try to match shader names in materials and assign the correct shaders to those.

## Other Usefull Tools

- [Asset Ripper GitHub Page](https://github.com/AssetRipper/AssetRipper)
- [AssetRipper Guid Patcher GitHub Page](https://github.com/ChrisFeline/AssetRipperGuidPatcher/tree/main)

## How To (Guide)

1. Use [Asset Ripper](https://github.com/AssetRipper/AssetRipper) to unpack game project 
2. Install this Script to Project Asset directory
3. Install [AssetRipper Guid Patcher](https://github.com/ChrisFeline/AssetRipperGuidPatcher/tree/main) to project directory
4. Install packages that used by this project
5. Then go to the top bar and run: `Kittenji` / `AssetRipper Guid Patch`
6. Wait for ending of process
7. Then go to the top bar and run: `Max9126` / `Fix Script References`
8. Wait for ending of process
9. Last Log should contain list of `Plugin Name` - `Plugin file GUID`
10. you should delete all plugins in `Assets/Plugins` Folder that not in this list
11. Done