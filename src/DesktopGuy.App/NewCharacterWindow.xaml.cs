using System;
using System.Globalization;
using System.Windows;
using DesktopGuy.App.Engine;
using Microsoft.Win32;

namespace DesktopGuy.App;

/// <summary>
/// The "New Character..." dialog. On success, closes with DialogResult
/// true and exposes the newly-created folder name via ImportedFolderName
/// so the caller can offer to switch to it right away.
/// </summary>
public partial class NewCharacterWindow : Window
{
    public string? ImportedFolderName { get; private set; }

    public NewCharacterWindow()
    {
        InitializeComponent();
    }

    private void OnBrowseClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose a sprite sheet PNG",
            Filter = "PNG images (*.png)|*.png",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) == true)
        {
            FilePathTextBox.Text = dialog.FileName;
        }
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OnImportClicked(object sender, RoutedEventArgs e)
    {
        HideError();

        if (!int.TryParse(ColumnsTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int columns) ||
            !int.TryParse(RowsTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rows))
        {
            ShowError("Columns and rows both need to be whole numbers.");
            return;
        }

        ImportButton.IsEnabled = false;
        try
        {
            ImportedFolderName = CharacterImporter.Import(
                NameTextBox.Text, FilePathTextBox.Text, columns, rows);
            DialogResult = true;
        }
        catch (CharacterImportException ex)
        {
            ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            ShowError($"Something unexpected went wrong: {ex.Message}");
        }
        finally
        {
            ImportButton.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void HideError()
    {
        ErrorText.Visibility = Visibility.Collapsed;
    }
}
