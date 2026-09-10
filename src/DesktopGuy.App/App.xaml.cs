using System;
using System.Windows;
using DesktopGuy.App.Characters;

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
    /// template to run; falls back to whichever character is found first.
    /// </summary>
    private static CharacterDefinition LoadRequestedCharacter(string[] args)
    {
        int flagIndex = Array.IndexOf(args, "--character");
        if (flagIndex >= 0 && flagIndex + 1 < args.Length)
        {
            return CharacterLoader.Load(args[flagIndex + 1]);
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
