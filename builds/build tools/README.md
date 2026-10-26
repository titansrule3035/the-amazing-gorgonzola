# How To Build The Amazing Gorgonzola:

1. Clone the GItHub repo, remember the path to this folder
2. Check the file called README.md in the repository, at the top you will see the Godot and .NET versions used (Godot .NET 4.7.2 and .NET SDK 10.0 as of right now)
3. Download [Godot .NET version 4.7.2](https://downloads.godotengine.org/?version=4.7.2&flavor=stable&slug=mono_win64.zip&platform=windows.64) and [.NET SDK version 10.0](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-10.0.100-windows-x64-installer)
4. Extract Godot and install the .NET SDK
5. Download the [Godot build tools for .NET](https://downloads.godotengine.org/?version=4.7.2&flavor=stable&slug=mono_export_templates.tpz&platform=templates)
6. Move this file into the repository, then into the folder called **builds** and then **build tools**
7. Rename this file to **godot_build_templates_dotnet** (keep the file extension the same as before!) 
8. Reboot
9. Go into the repository, then into **builds->build tools**
10. Edit the file called **GODOT_PATH.txt** and paste your path to your Godot executable (ex: **C:\Program Files\Godot\Godot_v4.7-stable_mono_win64**)
11. Save the file and close it
12. Run the file called **Setup.bat**
13. Once finished, go back to the repository, then into **builds->win**
14. Run TheAmazingGorgonzola.exe

You only need to do this setup one time unless the language or engine versions change, in which case I will let you know.

If you want to build a new version, just pull changes from GitHub, then run **Setup.bat** again

# **PLEASE DO NOT PUBLICALLY SHARE EXPORTS BUILT ON YOUR MACHINES!!!!!!!**