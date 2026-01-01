using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace acomp;

public class BoolToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return (bool)value ? "▼" : "▶";
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public class GroupItem
    {
        public string Name { get; set; } = "";
        public ObservableCollection<EmptyFile> Items { get; set; } = new();
    }

    public class EmptyFile
    {
        public string Path { get; set; } = "";
        public bool IsDeleted { get; set; } = false;
        public string OriginalPath { get; set; } = "";
        public string ParentDirectory { get; set; } = "";
    }

    private ObservableCollection<EmptyFile> emptyFiles = new();
    private ObservableCollection<GroupItem> groups = new();

    public MainWindow()
    {
        InitializeComponent();
        FilesItemsControl.ItemsSource = groups;
    }

    private void AddFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog() == true)
        {
            FoldersListBox.Items.Add(dialog.FolderName);
        }
    }

    private void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        var folders = FoldersListBox.Items.Cast<string>().ToList();
        if (!folders.Any())
        {
            System.Windows.MessageBox.Show("请先添加文件夹", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        emptyFiles.Clear();
        foreach (var folder in folders)
        {
            ScanFolder(folder);
        }

        // Group by parent directory
        var grouped = emptyFiles.GroupBy(f => f.ParentDirectory).Select(g => new GroupItem { Name = g.Key, Items = new ObservableCollection<EmptyFile>(g) });
        groups.Clear();
        foreach (var group in grouped)
        {
            groups.Add(group);
        }

        SelectionPanel.Visibility = Visibility.Collapsed;
        ResultsBorder.Visibility = Visibility.Visible;
        RescanButton.Visibility = Visibility.Visible;

        if (!emptyFiles.Any())
        {
            ResultsTitle.Text = "未找到0KB大小的文件。";
        }
    }

    private void ScanFolder(string rootDir)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(rootDir, "*", SearchOption.AllDirectories))
            {
                var fi = new FileInfo(file);
                if (fi.Length == 0)
                {
                    emptyFiles.Add(new EmptyFile { Path = file, OriginalPath = file, ParentDirectory = System.IO.Path.GetDirectoryName(file) ?? "" });
                }
            }
        }
        catch
        {
            // Skip inaccessible folders
        }
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is EmptyFile file)
        {
            string folder = System.IO.Path.GetDirectoryName(file.OriginalPath) ?? "";
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file.OriginalPath}\""));
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is EmptyFile file)
        {
            if (MoveToRecycleBin(file.OriginalPath))
            {
                file.IsDeleted = true;
                file.Path = "[已删除到回收站] " + file.OriginalPath;
                // Update UI
                FilesItemsControl.Items.Refresh();
            }
            else
            {
                System.Windows.MessageBox.Show("删除失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void RescanButton_Click(object sender, RoutedEventArgs e)
    {
        SelectionPanel.Visibility = Visibility.Visible;
        ResultsBorder.Visibility = Visibility.Collapsed;
        RescanButton.Visibility = Visibility.Collapsed;
        emptyFiles.Clear();
        groups.Clear();
        FoldersListBox.Items.Clear();
    }

    private void FoldersListBox_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            foreach (string file in files)
            {
                if (Directory.Exists(file))
                {
                    FoldersListBox.Items.Add(file);
                }
            }
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string pTo;
        public ushort fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string lpszProgressTitle;
    }

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;

    private bool MoveToRecycleBin(string path)
    {
        SHFILEOPSTRUCT shf = new SHFILEOPSTRUCT();
        shf.wFunc = FO_DELETE;
        shf.pFrom = path + '\0' + '\0';
        shf.fFlags = (ushort)(FOF_ALLOWUNDO | FOF_NOCONFIRMATION);
        int result = SHFileOperation(ref shf);
        return result == 0;
    }
}