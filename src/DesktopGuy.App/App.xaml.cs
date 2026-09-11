using System;
using System.Windows;
using DesktopGuy.App.Characters;
using DesktopGuy.App.Engine;

namespace DesktopGuy.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var definition = LoadRequestedCharacter(e.Args);

        var window = new MainWindow(definition);
        window.Show();
    }

    /// <summary>
    /// Supports `DesktopGuy.exe --character SomeFolderName` to pick which
    /// template to run. Without that flag, falls back to whichever
    /// character was last picked from the context menu's switcher (see
    /// CharacterPreferenceStore), then to whichever character is found
    /// first if nothing's been picked yet.
    /// </summary>
    private static CharacterDefinition LoadRequestedCharacter(string[] args)
    {
        int flagIndex = Array.IndexOf(args, "--character");
        if (flagIndex >= 0 && flagIndex + 1 < args.Length)
        {
            return CharacterLoader.Load(args[flagIndex + 1]);
        }

        var remembered = CharacterPreferenceStore.TryLoad();
        if (remembered is not null)
        {
            try
            {
                return CharacterLoader.Load(remembered);
            }
            catch
            {
                // The remembered folder may have been renamed or removed -
                // fall through to the default below rather than crash.
            }
        }

        try
        {
            return CharacterLoader.LoadDefault();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Couldn't load a character:\n{ex.Message}",
                "Desktop Guy",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            throw;
        }
    }
}
