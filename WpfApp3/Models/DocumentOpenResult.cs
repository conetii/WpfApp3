using System.Text;

namespace WpfApp3.Models;

public sealed record DocumentOpenResult(bool IsBinaryPreview, bool IsReadOnlyPreview, string Content, Encoding? Encoding, long FileSize);
