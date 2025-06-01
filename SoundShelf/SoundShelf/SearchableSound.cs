using System.ComponentModel;
using System.Runtime.CompilerServices;
using SoundShelf.Library;

namespace SoundShelf
{
    public record SearchableSound(string SoundId, string Label, List<string> Tags, SoundFile SoundFile) : INotifyPropertyChanged
    {
        private bool _isInShortList;
        public string SearchKey { get; } = $"{string.Join("|", Tags)}|{Label}".ToLowerInvariant();

        public bool IsInShortList
        {
            get => _isInShortList;
            set => SetField(ref _isInShortList, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
