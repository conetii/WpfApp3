using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfApp3.Models;

public sealed class StorageNode : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _isSelected;

    public StorageNode(string name, string fullPath, bool isDirectory, IEnumerable<StorageNode>? children = null)
    {
        Name = name;
        FullPath = fullPath;
        IsDirectory = isDirectory;
        Children = children is null ? [] : new ObservableCollection<StorageNode>(children);
    }

    public string Name { get; }

    public string FullPath { get; }

    public bool IsDirectory { get; }

    public ObservableCollection<StorageNode> Children { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
            {
                return;
            }

            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
