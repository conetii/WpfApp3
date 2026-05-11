using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using WpfApp3.Dialogs;
using WpfApp3.Models;
using WpfApp3.Services;

namespace WpfApp3.Pages;

public partial class WorkspacePage : Page
{
    private readonly StorageService _storageService = new();
    private readonly DocumentFileService _documentFileService = new();
    private readonly List<int> _logicalLineStarts = [0];
    private readonly DispatcherTimer _searchDebounceTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };

    private bool _isApplyingDocument;
    private bool _isRefreshingTree;
    private bool _isContextSelectingTreeNode;
    private bool _lineNumbersDirty = true;
    private string? _storageFolderPath;
    private string? _currentFilePath;
    private string? _pendingDocumentPath;
    private string? _selectedTreePath;
    private string _lastSavedText = string.Empty;
    private string _searchQuery = string.Empty;
    private Encoding _currentEncoding = new UTF8Encoding(false);
    private bool _isBinaryPreview;
    private bool _isReadOnlyPreview;
    private bool _isCurrentFileOutsideStorage;
    private int _searchLoadVersion;
    private CancellationTokenSource? _searchLoadCancellation;
    private StorageNode? _contextMenuNode;
    private ScrollViewer? _editorScrollViewer;

    public WorkspacePage(StorageReference? storageReference)
    {
        InitializeComponent();
        DataContext = this;
        Nodes = [];
        InitializeStorageState(storageReference);
        LineNumbersPresenter.TargetTextBox = EditorTextBox;
        LineNumbersPresenter.SetLogicalLineStarts(_logicalLineStarts);
        EditorTextBox.SizeChanged += EditorTextBox_SizeChanged;
        _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;
        Loaded += WorkspacePage_Loaded;
        Unloaded += WorkspacePage_Unloaded;
        ApplyLocalization();
        LoadStorageTree();
        ResetDocumentState();
    }

    public ObservableCollection<StorageNode> Nodes { get; }

    public event EventHandler? StateChanged;

    public bool CanUndoEdit => !_isReadOnlyPreview && EditorTextBox.CanUndo;

    public bool CanRedoEdit => !_isReadOnlyPreview && EditorTextBox.CanRedo;

    public bool CanDeleteCurrentDocument => !string.IsNullOrWhiteSpace(_currentFilePath);

    public void ChooseStorageFolder()
    {
        using Forms.FolderBrowserDialog dialog = new()
        {
            Description = Localize("ChooseStorageDialogDescription"),
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            InitialDirectory = GetInitialFolderDirectory()
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK)
        {
            return;
        }

        OpenFolderStorage(dialog.SelectedPath);
    }

    public void OpenDocumentFile()
    {
        Microsoft.Win32.OpenFileDialog dialog = new()
        {
            CheckFileExists = true,
            Title = LocalizeOrDefault("OpenDocumentDialogTitle", "РћС‚РєСЂС‹С‚СЊ С„Р°Р№Р»"),
            Filter = LocalizeOrDefault("DocumentDialogFilter", "All files (*.*)|*.*|Text documents (*.txt)|*.txt"),
            InitialDirectory = GetInitialDocumentDirectory()
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (!ResolvePendingChanges())
        {
            return;
        }

        OpenFile(dialog.FileName, promptToSwitchStorageIfNeeded: true);
    }

    public bool OpenDocumentFile(string? filePath)
    {
        string? normalizedFilePath = NormalizeExistingFilePath(filePath);

        if (string.IsNullOrWhiteSpace(normalizedFilePath))
        {
            return false;
        }

        if (!ResolvePendingChanges())
        {
            return false;
        }

        return OpenFile(normalizedFilePath, promptToSwitchStorageIfNeeded: false);
    }

    public bool CreateDocumentFile(string? filePath)
    {
        string? normalizedFilePath = NormalizeFullPath(filePath);

        if (string.IsNullOrWhiteSpace(normalizedFilePath)
            || File.Exists(normalizedFilePath)
            || Directory.Exists(normalizedFilePath))
        {
            return false;
        }

        if (!ResolvePendingChanges())
        {
            return false;
        }

        return CreateEmptyDocumentFile(normalizedFilePath, promptToSwitchStorageIfNeeded: false);
    }

    public void NewDocument()
    {
        if (!EnsureStorageFolderReady())
        {
            return;
        }

        if (!ResolvePendingChanges())
        {
            return;
        }

        _pendingDocumentPath = _storageService.SuggestNewFilePath(_storageFolderPath!, _selectedTreePath, Localize("UntitledDocument"));
        _currentFilePath = null;
        _currentEncoding = new UTF8Encoding(false);
        SetPreviewMode(false, false);
        _lastSavedText = string.Empty;
        UpdateCurrentFileStorageState(_pendingDocumentPath);
        ApplyEditorText(string.Empty);
        UpdateDocumentPresentation();
        EditorTextBox.Focus();
    }

    public bool SaveCurrentDocument()
    {
        if (_isReadOnlyPreview)
        {
            ShowInfo("MessageBinarySaveUnavailable");
            return false;
        }

        string? targetPath = GetDefaultSaveTargetPath();

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return false;
        }

        return SaveDocument(targetPath, promptToSwitchStorageIfNeeded: false);
    }

    public bool SaveCurrentDocumentAs()
    {
        if (_isReadOnlyPreview)
        {
            ShowInfo("MessageBinarySaveUnavailable");
            return false;
        }

        Microsoft.Win32.SaveFileDialog dialog = new()
        {
            Title = LocalizeOrDefault("SaveDocumentAsDialogTitle", "РЎРѕС…СЂР°РЅРёС‚СЊ РєР°Рє"),
            Filter = LocalizeOrDefault("DocumentDialogFilter", "All files (*.*)|*.*|Text documents (*.txt)|*.txt"),
            DefaultExt = GetSuggestedSaveFileExtension(),
            AddExtension = true,
            OverwritePrompt = true,
            InitialDirectory = GetInitialDocumentDirectory(),
            FileName = GetSuggestedSaveFileName()
        };

        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        return SaveDocument(dialog.FileName, promptToSwitchStorageIfNeeded: true);
    }

    private bool SaveDocument(string targetPath, bool promptToSwitchStorageIfNeeded)
    {
        if (IsPathInCurrentStorage(targetPath) && !EnsureStorageFolderReady())
        {
            return false;
        }

        bool existedBefore = File.Exists(targetPath);
        string? backupPath = null;
        string? pendingBackupPath = null;

        try
        {
            if (existedBefore)
            {
                pendingBackupPath = BuildBackupPath(targetPath);
                File.Copy(targetPath, pendingBackupPath, true);
                backupPath = pendingBackupPath;
            }

            string? directory = Path.GetDirectoryName(targetPath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _documentFileService.SaveText(targetPath, EditorTextBox.Text, _currentEncoding);

            if (ShouldCommitCurrentStorageChange(targetPath)
                && !CommitEncryptedStorageChange(
                    () => RestoreSavedFileFromBackup(targetPath, existedBefore, backupPath),
                    "MessageEncryptedPackFailed"))
            {
                CleanupTemporaryBackup(backupPath);
                LoadStorageTree(_selectedTreePath ?? _currentFilePath);
                UpdateDocumentPresentation();
                return false;
            }

            CleanupTemporaryBackup(backupPath);
            _currentFilePath = targetPath;
            _pendingDocumentPath = null;
            _selectedTreePath = targetPath;
            _lastSavedText = EditorTextBox.Text;
            UpdateCurrentFileStorageState(targetPath);
            LoadStorageTree(targetPath);
            UpdateDocumentPresentation();

            if (promptToSwitchStorageIfNeeded)
            {
                PromptToSwitchStorageIfNeeded(targetPath);
            }

            return true;
        }
        catch
        {
            RestoreSavedFileFromBackup(targetPath, existedBefore, backupPath);
            CleanupTemporaryBackup(backupPath ?? pendingBackupPath);
            ShowError("MessageSaveFileFailed");
            return false;
        }
    }

    private bool CreateEmptyDocumentFile(string targetPath, bool promptToSwitchStorageIfNeeded)
    {
        try
        {
            if (File.Exists(targetPath) || Directory.Exists(targetPath))
            {
                ShowInfo("MessageFileAlreadyExists");
                return false;
            }

            string? directory = Path.GetDirectoryName(targetPath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _documentFileService.SaveText(targetPath, string.Empty, new UTF8Encoding(false));

            if (ShouldCommitCurrentStorageChange(targetPath)
                && !CommitEncryptedStorageChange(
                    () => DeleteIfExists(targetPath),
                    "MessageEncryptedPackFailed"))
            {
                LoadStorageTree(_selectedTreePath ?? _currentFilePath);
                return false;
            }

            bool opened = OpenFile(targetPath, promptToSwitchStorageIfNeeded: promptToSwitchStorageIfNeeded, showBinaryPreviewInfo: false);

            if (opened)
            {
                EditorTextBox.Focus();
            }

            return opened;
        }
        catch
        {
            ShowError("MessageCreateFileFailed");
            return false;
        }
    }

    public bool CloseCurrentDocument()
    {
        if (!ResolvePendingChanges())
        {
            return false;
        }

        ResetDocumentState();
        LoadStorageTree(_selectedTreePath);
        return true;
    }

    public bool DeleteCurrentDocument()
    {
        if (string.IsNullOrWhiteSpace(_currentFilePath))
        {
            ShowInfo("MessageNoFileToDelete");
            return false;
        }

        return DeleteFile(_currentFilePath);
    }

    public bool TryCloseApplication()
    {
        return TryCloseStorageSessionForExit();
    }

    public bool TryHandleEscape()
    {
        if (SearchPanel.Visibility == Visibility.Visible)
        {
            ClearSearch();
            return true;
        }

        return false;
    }

    public void UndoEdit()
    {
        if (_isReadOnlyPreview)
        {
            return;
        }

        if (!ReferenceEquals(Keyboard.FocusedElement, EditorTextBox))
        {
            EditorTextBox.Focus();
        }

        if (CanUndoEdit)
        {
            EditorTextBox.Undo();
            UpdateDocumentPresentation();
        }
    }

    public void RedoEdit()
    {
        if (_isReadOnlyPreview)
        {
            return;
        }

        if (!ReferenceEquals(Keyboard.FocusedElement, EditorTextBox))
        {
            EditorTextBox.Focus();
        }

        if (CanRedoEdit)
        {
            EditorTextBox.Redo();
            UpdateDocumentPresentation();
        }
    }

    public void FocusSearch()
    {
        ShowSearchPanel();
        SearchTextBox.Focus();
        SearchTextBox.SelectAll();
    }

    public void ClearSearch()
    {
        _searchQuery = string.Empty;
        CancelPendingSearchRefresh();

        if (!string.IsNullOrEmpty(SearchTextBox.Text))
        {
            SearchTextBox.Text = string.Empty;
        }

        LoadStorageTree(_selectedTreePath ?? _currentFilePath);
        HideSearchPanel();
    }

    private void WorkspacePage_Loaded(object sender, RoutedEventArgs e)
    {
        LocalizationService.Instance.CultureChanged += LocalizationService_CultureChanged;
        ApplyLocalization();
        AttachEditorScrollViewers();
        LoadStorageTree(_selectedTreePath ?? _currentFilePath);
        ScheduleLineNumbersUpdate();
    }

    private void WorkspacePage_Unloaded(object sender, RoutedEventArgs e)
    {
        LocalizationService.Instance.CultureChanged -= LocalizationService_CultureChanged;
        CancelPendingSearchRefresh();
        DetachEditorScrollViewers();
    }

    private void LocalizationService_CultureChanged(object? sender, EventArgs e)
    {
        ApplyLocalization();
        LoadStorageTree(_selectedTreePath ?? _currentFilePath);
        ScheduleLineNumbersUpdate();
    }

    private void ChooseStorageButton_Click(object sender, RoutedEventArgs e)
    {
        ChooseStorageFolder();
    }

    private void UnlockStorageButton_Click(object sender, RoutedEventArgs e)
    {
        UnlockCurrentStorage();
    }

    private void CloseSearchButton_Click(object sender, RoutedEventArgs e)
    {
        ClearSearch();
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchQuery = SearchTextBox.Text;
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private async void SearchDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _searchDebounceTimer.Stop();
        await LoadStorageTreeForSearchAsync(_selectedTreePath ?? _currentFilePath);
    }

    private void StorageTreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _contextMenuNode = GetStorageNodeFromElement(e.OriginalSource as DependencyObject);

        if (_contextMenuNode is null)
        {
            UpdateTreeContextMenu();
            return;
        }

        _selectedTreePath = _contextMenuNode.FullPath;
        SelectTreeNode(_contextMenuNode.FullPath);
        UpdateTreeContextMenu();
    }

    private void StorageTreeView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (StorageTreeScrollViewer is null)
        {
            return;
        }

        double delta = e.Delta > 0 ? -48 : 48;

        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift || StorageTreeScrollViewer.ScrollableHeight <= 0)
        {
            double offset = Math.Max(0, Math.Min(StorageTreeScrollViewer.HorizontalOffset + delta, StorageTreeScrollViewer.ScrollableWidth));
            StorageTreeScrollViewer.ScrollToHorizontalOffset(offset);
        }
        else
        {
            double offset = Math.Max(0, Math.Min(StorageTreeScrollViewer.VerticalOffset + delta, StorageTreeScrollViewer.ScrollableHeight));
            StorageTreeScrollViewer.ScrollToVerticalOffset(offset);
        }

        e.Handled = true;
    }

    private void StorageTreeView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        StorageNode? nodeUnderPointer = GetStorageNodeFromElement(e.OriginalSource as DependencyObject) ?? GetStorageNodeUnderMouse();

        if (nodeUnderPointer is not null)
        {
            _contextMenuNode = nodeUnderPointer;
            _selectedTreePath = nodeUnderPointer.FullPath;
            SelectTreeNode(nodeUnderPointer.FullPath);
        }
        else if (!StorageTreeView.IsMouseOver)
        {
            _contextMenuNode = StorageTreeView.SelectedItem as StorageNode;
        }
        else
        {
            _contextMenuNode = null;
        }

        UpdateTreeContextMenu();
    }

    private void CreateTreeFileMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureStorageFolderReady())
        {
            return;
        }

        TextInputDialog dialog = new(
            Localize("FileNameDialogCreateTitle"),
            Localize("FileNameDialogCreatePrompt"),
            Localize("DialogCreate"),
            Localize("DialogCancel"),
            Localize("UntitledDocument"))
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (!ResolvePendingChanges())
        {
            return;
        }

        try
        {
            string targetPath = _storageService.BuildFilePath(_storageFolderPath!, _contextMenuNode?.FullPath, dialog.InputText);

            if (File.Exists(targetPath))
            {
                ShowInfo("MessageFileAlreadyExists");
                return;
            }

            CreateEmptyDocumentFile(targetPath, promptToSwitchStorageIfNeeded: false);
        }
        catch
        {
            ShowError("MessageCreateFileFailed");
        }
    }

    private void RenameTreeFileMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode is null || _contextMenuNode.IsDirectory)
        {
            return;
        }

        string sourcePath = _contextMenuNode.FullPath;

        if (string.Equals(_currentFilePath, sourcePath, StringComparison.OrdinalIgnoreCase) && HasUnsavedChanges && !ResolvePendingChanges())
        {
            return;
        }

        TextInputDialog dialog = new(
            Localize("FileNameDialogRenameTitle"),
            Localize("FileNameDialogRenamePrompt"),
            Localize("DialogRename"),
            Localize("DialogCancel"),
            Path.GetFileName(sourcePath))
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            string targetPath = _storageService.BuildRenamedFilePath(sourcePath, dialog.InputText);

            if (string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (File.Exists(targetPath))
            {
                ShowInfo("MessageFileAlreadyExists");
                return;
            }

            File.Move(sourcePath, targetPath);

            if (!CommitEncryptedStorageChange(
                    () => File.Move(targetPath, sourcePath),
                    "MessageEncryptedPackFailed"))
            {
                LoadStorageTree(sourcePath);
                return;
            }

            if (string.Equals(_currentFilePath, sourcePath, StringComparison.OrdinalIgnoreCase))
            {
                _currentFilePath = targetPath;
                UpdateCurrentFileStorageState(targetPath);
            }

            if (string.Equals(_selectedTreePath, sourcePath, StringComparison.OrdinalIgnoreCase))
            {
                _selectedTreePath = targetPath;
            }

            _contextMenuNode = new StorageNode(Path.GetFileName(targetPath), targetPath, false);
            LoadStorageTree(targetPath);
            UpdateDocumentPresentation();
        }
        catch
        {
            ShowError("MessageRenameFileFailed");
        }
    }

    private void DeleteTreeFileMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuNode is null || _contextMenuNode.IsDirectory)
        {
            return;
        }

        DeleteFile(_contextMenuNode.FullPath);
    }

    private void UnlockTreeStorageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        UnlockCurrentStorage();
    }

    private void StorageTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_isRefreshingTree || e.NewValue is not StorageNode node)
        {
            return;
        }

        string? previousSelection = _selectedTreePath;
        _selectedTreePath = node.FullPath;

        if (_isContextSelectingTreeNode)
        {
            RaiseStateChanged();
            return;
        }

        if (node.IsDirectory)
        {
            RaiseStateChanged();
            return;
        }

        if (string.Equals(_currentFilePath, node.FullPath, StringComparison.OrdinalIgnoreCase))
        {
            RaiseStateChanged();
            return;
        }

        if (!ResolvePendingChanges())
        {
            _selectedTreePath = previousSelection;
            LoadStorageTree(previousSelection ?? _currentFilePath);
            return;
        }

        OpenFile(node.FullPath);
    }

    private void EditorTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isApplyingDocument)
        {
            return;
        }

        _lineNumbersDirty = true;
        UpdateDocumentPresentation();
        ScheduleLineNumbersUpdate();
    }

    private void EditorTextBox_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ScheduleLineNumbersUpdate();
    }

    private void EditorScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange != 0 || e.ExtentHeightChange != 0 || e.ViewportHeightChange != 0 || e.ViewportWidthChange != 0 || e.ExtentWidthChange != 0)
        {
            ScheduleLineNumbersUpdate();
        }
    }

    private void CutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        EditorTextBox.Cut();
    }

    private void CopyMenuItem_Click(object sender, RoutedEventArgs e)
    {
        EditorTextBox.Copy();
    }

    private void PasteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        EditorTextBox.Paste();
    }

    private void SelectAllMenuItem_Click(object sender, RoutedEventArgs e)
    {
        EditorTextBox.SelectAll();
    }

    private bool OpenFile(string filePath, bool promptToSwitchStorageIfNeeded = false, bool showBinaryPreviewInfo = true)
    {
        try
        {
            DocumentOpenResult document = _documentFileService.Open(filePath);
            _currentFilePath = filePath;
            _pendingDocumentPath = null;
            _selectedTreePath = filePath;
            _currentEncoding = document.Encoding ?? new UTF8Encoding(false);
            SetPreviewMode(document.IsBinaryPreview, document.IsReadOnlyPreview);
            _lastSavedText = document.Content;
            UpdateCurrentFileStorageState(filePath);
            ApplyEditorText(document.Content);
            LoadStorageTree(filePath);
            UpdateDocumentPresentation();

            if (document.IsBinaryPreview && showBinaryPreviewInfo)
            {
                ShowInfo("MessageBinaryOpenedReadOnly");
            }

            if (promptToSwitchStorageIfNeeded)
            {
                PromptToSwitchStorageIfNeeded(filePath);
            }

            return true;
        }
        catch
        {
            ShowError("MessageReadFileFailed");
            return false;
        }
    }

    private void LoadStorageTree(string? preferredPath = null)
    {
        CancelPendingSearchRefresh();

        IReadOnlyList<StorageNode> loadedNodes = [];

        try
        {
            if (_storageService.StorageExists(_storageFolderPath))
            {
                loadedNodes = _storageService.LoadTree(_storageFolderPath!, _searchQuery);
            }
        }
        catch
        {
            ShowError("MessageLoadStorageFailed");
        }

        ApplyLoadedTree(loadedNodes, preferredPath);
    }

    private async Task LoadStorageTreeForSearchAsync(string? preferredPath)
    {
        string? storageFolderPath = _storageFolderPath;
        string searchQuery = _searchQuery;
        int requestVersion = ++_searchLoadVersion;

        _searchLoadCancellation?.Cancel();
        _searchLoadCancellation?.Dispose();
        CancellationTokenSource cancellation = new();
        _searchLoadCancellation = cancellation;

        try
        {
            IReadOnlyList<StorageNode> loadedNodes = [];

            if (_storageService.StorageExists(storageFolderPath))
            {
                loadedNodes = await Task.Run(() => _storageService.LoadTree(storageFolderPath!, searchQuery), cancellation.Token);
            }

            if (cancellation.IsCancellationRequested
                || requestVersion != _searchLoadVersion
                || !string.Equals(searchQuery, _searchQuery, StringComparison.Ordinal)
                || !string.Equals(storageFolderPath, _storageFolderPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ApplyLoadedTree(loadedNodes, preferredPath);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            if (requestVersion == _searchLoadVersion)
            {
                ShowError("MessageLoadStorageFailed");
            }
        }
        finally
        {
            if (ReferenceEquals(_searchLoadCancellation, cancellation))
            {
                _searchLoadCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private void ApplyLoadedTree(IReadOnlyList<StorageNode> loadedNodes, string? preferredPath)
    {
        Nodes.Clear();
        _isRefreshingTree = true;

        try
        {
            foreach (StorageNode node in loadedNodes)
            {
                Nodes.Add(node);
            }

            if (!string.IsNullOrWhiteSpace(preferredPath))
            {
                MarkSelection(Nodes, preferredPath);
            }
        }
        finally
        {
            _isRefreshingTree = false;
        }

        UpdateStoragePresentation();
        RaiseStateChanged();
    }

    private void CancelPendingSearchRefresh()
    {
        _searchDebounceTimer.Stop();
        _searchLoadVersion++;

        if (_searchLoadCancellation is null)
        {
            return;
        }

        _searchLoadCancellation.Cancel();
        _searchLoadCancellation.Dispose();
        _searchLoadCancellation = null;
    }

    private static bool MarkSelection(IEnumerable<StorageNode> nodes, string selectedPath)
    {
        foreach (StorageNode node in nodes)
        {
            node.IsSelected = false;
            node.IsExpanded = false;

            if (string.Equals(node.FullPath, selectedPath, StringComparison.OrdinalIgnoreCase))
            {
                node.IsSelected = true;
                return true;
            }

            if (MarkSelection(node.Children, selectedPath))
            {
                node.IsExpanded = true;
                return true;
            }
        }

        return false;
    }

    private bool ResolvePendingChanges()
    {
        if (!HasUnsavedChanges)
        {
            return true;
        }

        MessageBoxResult result = System.Windows.MessageBox.Show(
            Localize("MessageUnsavedChanges"),
            Localize("DialogConfirmTitle"),
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Cancel)
        {
            return false;
        }

        if (result == MessageBoxResult.No)
        {
            DiscardPendingChanges();
            return true;
        }

        return SaveCurrentDocument();
    }

    private void DiscardPendingChanges()
    {
        if (string.IsNullOrWhiteSpace(_currentFilePath) && !string.IsNullOrWhiteSpace(_pendingDocumentPath))
        {
            ResetDocumentState();
            return;
        }

        ApplyEditorText(_lastSavedText);
        UpdateDocumentPresentation();
    }

    private void ResetDocumentState()
    {
        _currentFilePath = null;
        _pendingDocumentPath = null;
        _currentEncoding = new UTF8Encoding(false);
        SetPreviewMode(false, false);
        _isCurrentFileOutsideStorage = false;
        _lastSavedText = string.Empty;
        ApplyEditorText(string.Empty);
        UpdateDocumentPresentation();
    }

    private void ApplyEditorText(string text)
    {
        _isApplyingDocument = true;
        EditorTextBox.Text = text;
        EditorTextBox.CaretIndex = EditorTextBox.Text.Length;
        _isApplyingDocument = false;
        _lineNumbersDirty = true;
        UpdateDocumentPresentation();
        ScheduleLineNumbersUpdate();
    }

    private void ShowSearchPanel()
    {
        SearchPanel.Visibility = Visibility.Visible;
    }

    private void HideSearchPanel()
    {
        SearchPanel.Visibility = Visibility.Collapsed;
    }

    private void ApplyLocalization()
    {
        StorageSelectorButton.ToolTip = Localize("StorageSelectorTooltip");
        StorageRootLabelTextBlock.Text = Localize("StorageRootLabel");
        UnlockStorageButton.Content = Localize("StorageUnlockButton");
        UnlockStorageButton.ToolTip = Localize("StorageUnlockTooltip");
        StorageTreeLabelTextBlock.Text = Localize("StorageTreeLabel");
        SearchTextBox.ToolTip = Localize("SearchLabel");
        CloseSearchButton.ToolTip = Localize("MenuClearSearch");
        StorageEmptyTextBlock.Text = string.IsNullOrWhiteSpace(_storageFolderPath)
            ? Localize("StorageUnavailableMessage")
            : Localize("StorageEmptyMessage");
        DocumentLabelTextBlock.Text = Localize("DocumentLabel");
        UnlockTreeStorageMenuItem.Header = Localize("TreeContextUnlockStorage");
        CreateTreeFileMenuItem.Header = Localize("TreeContextCreateFile");
        RenameTreeFileMenuItem.Header = Localize("TreeContextRenameFile");
        DeleteTreeFileMenuItem.Header = Localize("TreeContextDeleteFile");
        CutMenuItem.Header = Localize("EditCut");
        CopyMenuItem.Header = Localize("EditCopy");
        PasteMenuItem.Header = Localize("EditPaste");
        SelectAllMenuItem.Header = Localize("EditSelectAll");
        UpdateStoragePresentation();
        UpdateTreeContextMenu();
        UpdateDocumentPresentation();
        ScheduleLineNumbersUpdate();
    }

    private void UpdateStoragePresentation()
    {
        StoragePathTextBlock.Text = _storageReference is null
            ? Localize("NoStorageSelected")
            : _storageReference.SourcePath;

        bool hasNodes = Nodes.Count > 0;
        StorageEmptyTextBlock.Visibility = hasNodes ? Visibility.Collapsed : Visibility.Visible;
        StorageEmptyTextBlock.Text = GetStorageEmptyStateText();
        UnlockStorageButton.Visibility = CanUnlockCurrentStorage ? Visibility.Visible : Visibility.Collapsed;
        UnlockStorageButton.IsEnabled = CanUnlockCurrentStorage;
    }

    private void UpdateDocumentPresentation()
    {
        DocumentValueTextBlock.Text = GetDisplayedDocumentName();
        DocumentStatusTextBlock.Text = GetDocumentStatusText();
        RaiseStateChanged();
    }

    private void UpdateTreeContextMenu()
    {
        bool storageReady = _storageService.StorageExists(_storageFolderPath);
        bool hasFileTarget = storageReady && _contextMenuNode is { IsDirectory: false };
        UnlockTreeStorageMenuItem.Visibility = CanUnlockCurrentStorage ? Visibility.Visible : Visibility.Collapsed;
        UnlockTreeStorageMenuItem.IsEnabled = CanUnlockCurrentStorage;
        CreateTreeFileMenuItem.IsEnabled = storageReady;
        RenameTreeFileMenuItem.IsEnabled = hasFileTarget;
        DeleteTreeFileMenuItem.IsEnabled = hasFileTarget;
    }

    private void AttachEditorScrollViewers()
    {
        DetachEditorScrollViewers();
        EditorTextBox.ApplyTemplate();
        _editorScrollViewer = FindDescendant<ScrollViewer>(EditorTextBox);

        if (_editorScrollViewer is not null)
        {
            _editorScrollViewer.ScrollChanged += EditorScrollViewer_ScrollChanged;
        }

        ScheduleLineNumbersUpdate();
    }

    private void DetachEditorScrollViewers()
    {
        if (_editorScrollViewer is not null)
        {
            _editorScrollViewer.ScrollChanged -= EditorScrollViewer_ScrollChanged;
        }

        _editorScrollViewer = null;
    }

    private void ScheduleLineNumbersUpdate()
    {
        Dispatcher.BeginInvoke(UpdateLineNumbers, DispatcherPriority.Background);
    }

    private void UpdateLineNumbers()
    {
        if (_lineNumbersDirty)
        {
            RefreshLogicalLineStarts();
            _lineNumbersDirty = false;
        }

        LineNumbersPresenter.InvalidateVisual();
    }

    private void SetPreviewMode(bool isBinaryPreview, bool isReadOnlyPreview)
    {
        _isBinaryPreview = isBinaryPreview;
        _isReadOnlyPreview = isReadOnlyPreview;
        EditorTextBox.IsReadOnly = isReadOnlyPreview;
    }

    private static void RestoreSavedFileFromBackup(string targetPath, bool existedBefore, string? backupPath)
    {
        if (existedBefore)
        {
            if (!string.IsNullOrWhiteSpace(backupPath))
            {
                RestoreDeletedFile(backupPath, targetPath);
            }

            return;
        }

        DeleteIfExists(targetPath);
    }

    private static void CleanupTemporaryBackup(string? backupPath)
    {
        if (!string.IsNullOrWhiteSpace(backupPath))
        {
            DeleteIfExists(backupPath);
        }
    }

    private void RefreshLogicalLineStarts()
    {
        _logicalLineStarts.Clear();
        _logicalLineStarts.Add(0);
        string text = EditorTextBox.Text;

        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];

            if (character == '\r')
            {
                if (index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }

                _logicalLineStarts.Add(index + 1);
                continue;
            }

            if (character == '\n')
            {
                _logicalLineStarts.Add(index + 1);
            }
        }

        LineNumbersPresenter.SetLogicalLineStarts(_logicalLineStarts);
    }

    private string GetDisplayedDocumentName()
    {
        if (!string.IsNullOrWhiteSpace(_currentFilePath))
        {
            return Path.GetFileName(_currentFilePath);
        }

        if (!string.IsNullOrWhiteSpace(_pendingDocumentPath))
        {
            return Path.GetFileName(_pendingDocumentPath);
        }

        return Localize("DocumentEmpty");
    }

    private string GetDocumentStatusText()
    {
        if (_isCurrentFileOutsideStorage)
        {
            if (_isReadOnlyPreview)
            {
                return LocalizeOrDefault("StatusOutsideStorageReadOnly", "РўРѕР»СЊРєРѕ С‡С‚РµРЅРёРµ, С„Р°Р№Р» РІРЅРµ РґРµСЂРµРІР° С…СЂР°РЅРёР»РёС‰Р°");
            }

            if (HasUnsavedChanges)
            {
                return LocalizeOrDefault("StatusOutsideStorageUnsaved", "Р•СЃС‚СЊ РёР·РјРµРЅРµРЅРёСЏ, С„Р°Р№Р» РІРЅРµ РґРµСЂРµРІР° С…СЂР°РЅРёР»РёС‰Р°");
            }

            return LocalizeOrDefault("StatusOutsideStorage", "Р¤Р°Р№Р» РІРЅРµ РґРµСЂРµРІР° С…СЂР°РЅРёР»РёС‰Р°");
        }

        return _isReadOnlyPreview
            ? Localize("StatusBinaryPreview")
            : HasUnsavedChanges ? Localize("StatusUnsaved") : Localize("StatusSaved");
    }

    private string? GetDefaultSaveTargetPath()
    {
        if (!string.IsNullOrWhiteSpace(_currentFilePath))
        {
            return _currentFilePath;
        }

        if (!string.IsNullOrWhiteSpace(_pendingDocumentPath))
        {
            return _pendingDocumentPath;
        }

        if (!EnsureStorageFolderReady())
        {
            return null;
        }

        return _storageService.SuggestNewFilePath(_storageFolderPath!, _selectedTreePath, Localize("UntitledDocument"));
    }

    private void UpdateCurrentFileStorageState(string? filePath)
    {
        _isCurrentFileOutsideStorage = IsFileOutsideCurrentStorage(filePath);
    }

    private void PromptToSwitchStorageIfNeeded(string filePath)
    {
        if (!IsFileOutsideCurrentStorage(filePath))
        {
            _isCurrentFileOutsideStorage = false;
            UpdateDocumentPresentation();
            return;
        }

        _isCurrentFileOutsideStorage = true;
        UpdateDocumentPresentation();

        string? fileDirectory = Path.GetDirectoryName(filePath);

        if (string.IsNullOrWhiteSpace(fileDirectory))
        {
            return;
        }

        MessageBoxResult result = System.Windows.MessageBox.Show(
            LocalizeOrDefault("MessageFileOutsideStoragePrompt", "Р¤Р°Р№Р» РЅР°С…РѕРґРёС‚СЃСЏ РІРЅРµ С‚РµРєСѓС‰РµРіРѕ С…СЂР°РЅРёР»РёС‰Р°. РџРµСЂРµРєР»СЋС‡РёС‚СЊСЃСЏ РЅР° РїР°РїРєСѓ С„Р°Р№Р»Р°?"),
            Localize("DialogConfirmTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.Yes);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        if (!SwitchToFileDirectoryStorage(filePath))
        {
            UpdateCurrentFileStorageState(filePath);
            UpdateDocumentPresentation();
            return;
        }

        OpenFile(filePath, promptToSwitchStorageIfNeeded: false, showBinaryPreviewInfo: false);
    }

    private string GetInitialDocumentDirectory()
    {
        string? candidatePath = _currentFilePath ?? _pendingDocumentPath;

        if (!string.IsNullOrWhiteSpace(candidatePath))
        {
            string? candidateDirectory = Path.GetDirectoryName(candidatePath);

            if (!string.IsNullOrWhiteSpace(candidateDirectory) && Directory.Exists(candidateDirectory))
            {
                return candidateDirectory;
            }
        }

        if (_storageService.StorageExists(_storageFolderPath))
        {
            return _storageFolderPath!;
        }

        return GetInitialFolderDirectory();
    }

    private string GetSuggestedSaveFileName()
    {
        string? candidatePath = _currentFilePath ?? _pendingDocumentPath;

        if (!string.IsNullOrWhiteSpace(candidatePath))
        {
            return Path.GetFileName(candidatePath);
        }

        string baseName = Localize("UntitledDocument");
        return Path.HasExtension(baseName) ? baseName : $"{baseName}.txt";
    }

    private string GetSuggestedSaveFileExtension()
    {
        string? candidatePath = _currentFilePath ?? _pendingDocumentPath;
        string extension = string.IsNullOrWhiteSpace(candidatePath) ? string.Empty : Path.GetExtension(candidatePath);
        return string.IsNullOrWhiteSpace(extension) ? ".txt" : extension;
    }

    private bool EnsureStorageFolderReady()
    {
        if (_storageService.StorageExists(_storageFolderPath))
        {
            return true;
        }

        if (_storageReference?.Kind == StorageKind.EncryptedArchive)
        {
            ShowInfo("MessageUnlockStorageFirst");
            return false;
        }

        ShowInfo("MessageSelectStorageFirst");
        return false;
    }

    private bool DeleteFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        bool isCurrentDocument = string.Equals(_currentFilePath, filePath, StringComparison.OrdinalIgnoreCase);

        if (isCurrentDocument && HasUnsavedChanges && !ResolvePendingChanges())
        {
            return false;
        }

        MessageBoxResult result = System.Windows.MessageBox.Show(
            string.Format(Localize("MessageDeleteNamedFileConfirm"), Path.GetFileName(filePath)),
            Localize("DialogConfirmTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return false;
        }

        string? parentPath = Path.GetDirectoryName(filePath);

        try
        {
            if (ShouldCommitCurrentStorageChange(filePath))
            {
                string backupPath = BuildBackupPath(filePath);
                File.Copy(filePath, backupPath, true);
                File.Delete(filePath);

                if (!CommitEncryptedStorageChange(
                        () => RestoreDeletedFile(backupPath, filePath),
                        "MessageEncryptedPackFailed"))
                {
                    LoadStorageTree(filePath);
                    return false;
                }

                DeleteIfExists(backupPath);
            }
            else
            {
                File.Delete(filePath);
            }

            if (isCurrentDocument)
            {
                _selectedTreePath = parentPath;
                ResetDocumentState();
            }
            else if (string.Equals(_selectedTreePath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                _selectedTreePath = parentPath;
            }

            _contextMenuNode = null;
            LoadStorageTree(_selectedTreePath ?? parentPath);
            return true;
        }
        catch
        {
            ShowError("MessageDeleteFileFailed");
            return false;
        }
    }

    private void SelectTreeNode(string selectedPath)
    {
        _isContextSelectingTreeNode = true;
        MarkSelection(Nodes, selectedPath);
        _isContextSelectingTreeNode = false;
    }

    private StorageNode? GetStorageNodeUnderMouse()
    {
        System.Windows.Point position = Mouse.GetPosition(StorageTreeView);
        HitTestResult? hit = VisualTreeHelper.HitTest(StorageTreeView, position);
        return GetStorageNodeFromElement(hit?.VisualHit);
    }

    private static StorageNode? GetStorageNodeFromElement(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is FrameworkElement frameworkElement && frameworkElement.DataContext is StorageNode node)
            {
                return node;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return null;
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        int childrenCount = VisualTreeHelper.GetChildrenCount(root);

        for (int index = 0; index < childrenCount; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);

            if (child is T typedChild)
            {
                return typedChild;
            }

            T? descendant = FindDescendant<T>(child);

            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private bool HasUnsavedChanges
        => !_isReadOnlyPreview
           && (!string.IsNullOrWhiteSpace(_pendingDocumentPath)
               || NormalizeText(EditorTextBox.Text) != NormalizeText(_lastSavedText));

    private static string NormalizeText(string text)
    {
        return text.Replace("\r\n", "\n").Replace("\r", "\n");
    }

    private string Localize(string key)
    {
        return LocalizationService.Instance[key];
    }

    private string LocalizeOrDefault(string key, string fallback)
    {
        string localized = Localize(key);
        return string.Equals(localized, key, StringComparison.Ordinal) ? fallback : localized;
    }

    private void ShowInfo(string key)
    {
        System.Windows.MessageBox.Show(Localize(key), Localize("DialogInfoTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowError(string key)
    {
        ShowError(key, null);
    }

    private void ShowError(string key, string? details)
    {
        string message = Localize(key);

        if (!string.IsNullOrWhiteSpace(details))
        {
            message += Environment.NewLine + Environment.NewLine + details.Trim();
        }

        System.Windows.MessageBox.Show(message, Localize("DialogErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}


