using System.Globalization;

namespace WpfApp3.Controls;

public sealed class LineNumbersPresenter : System.Windows.FrameworkElement
{
    public static readonly System.Windows.DependencyProperty TargetTextBoxProperty = System.Windows.DependencyProperty.Register(
        nameof(TargetTextBox),
        typeof(System.Windows.Controls.TextBox),
        typeof(LineNumbersPresenter),
        new System.Windows.FrameworkPropertyMetadata(null, System.Windows.FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly System.Windows.DependencyProperty ForegroundProperty = System.Windows.Documents.TextElement.ForegroundProperty.AddOwner(
        typeof(LineNumbersPresenter),
        new System.Windows.FrameworkPropertyMetadata(System.Windows.Media.Brushes.Gray, System.Windows.FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly System.Windows.DependencyProperty FontFamilyProperty = System.Windows.Documents.TextElement.FontFamilyProperty.AddOwner(
        typeof(LineNumbersPresenter),
        new System.Windows.FrameworkPropertyMetadata(System.Windows.SystemFonts.MessageFontFamily, System.Windows.FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly System.Windows.DependencyProperty FontSizeProperty = System.Windows.Documents.TextElement.FontSizeProperty.AddOwner(
        typeof(LineNumbersPresenter),
        new System.Windows.FrameworkPropertyMetadata(System.Windows.SystemFonts.MessageFontSize, System.Windows.FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly System.Windows.DependencyProperty FontWeightProperty = System.Windows.Documents.TextElement.FontWeightProperty.AddOwner(
        typeof(LineNumbersPresenter),
        new System.Windows.FrameworkPropertyMetadata(System.Windows.FontWeights.Normal, System.Windows.FrameworkPropertyMetadataOptions.AffectsRender));

    private IReadOnlyList<int> _logicalLineStarts = [0];

    public System.Windows.Controls.TextBox? TargetTextBox
    {
        get => (System.Windows.Controls.TextBox?)GetValue(TargetTextBoxProperty);
        set => SetValue(TargetTextBoxProperty, value);
    }

    public System.Windows.Media.Brush Foreground
    {
        get => (System.Windows.Media.Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public System.Windows.Media.FontFamily FontFamily
    {
        get => (System.Windows.Media.FontFamily)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public System.Windows.FontWeight FontWeight
    {
        get => (System.Windows.FontWeight)GetValue(FontWeightProperty);
        set => SetValue(FontWeightProperty, value);
    }

    public void SetLogicalLineStarts(IReadOnlyList<int>? logicalLineStarts)
    {
        _logicalLineStarts = logicalLineStarts is { Count: > 0 } ? logicalLineStarts : [0];
        InvalidateVisual();
    }

    protected override void OnRender(System.Windows.Media.DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (TargetTextBox is null || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        int firstVisibleLineIndex = TargetTextBox.GetFirstVisibleLineIndex();
        int lastVisibleLineIndex = TargetTextBox.GetLastVisibleLineIndex();

        if (firstVisibleLineIndex < 0 || lastVisibleLineIndex < firstVisibleLineIndex)
        {
            return;
        }

        System.Windows.Media.Typeface typeface = new(
            FontFamily,
            System.Windows.FontStyles.Normal,
            FontWeight,
            System.Windows.FontStretches.Normal);
        double pixelsPerDip = System.Windows.Media.VisualTreeHelper.GetDpi(this).PixelsPerDip;

        for (int visualLineIndex = firstVisibleLineIndex; visualLineIndex <= lastVisibleLineIndex; visualLineIndex++)
        {
            int characterIndex = TargetTextBox.GetCharacterIndexFromLineIndex(visualLineIndex);

            if (!TryGetLogicalLineNumber(characterIndex, out int logicalLineNumber))
            {
                continue;
            }

            System.Windows.Rect characterRect = GetCharacterRect(TargetTextBox, characterIndex);

            if (characterRect.IsEmpty)
            {
                continue;
            }

            System.Windows.Point topLeft = TargetTextBox.TranslatePoint(new System.Windows.Point(characterRect.Left, characterRect.Top), this);

            if (topLeft.Y + characterRect.Height < 0 || topLeft.Y > ActualHeight)
            {
                continue;
            }

            System.Windows.Media.FormattedText formattedText = new(
                logicalLineNumber.ToString(CultureInfo.CurrentCulture),
                CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                typeface,
                FontSize,
                Foreground,
                pixelsPerDip);

            double x = Math.Max(0, ActualWidth - formattedText.Width - 4);
            drawingContext.DrawText(formattedText, new System.Windows.Point(x, topLeft.Y));
        }
    }

    private bool TryGetLogicalLineNumber(int characterIndex, out int logicalLineNumber)
    {
        int left = 0;
        int right = _logicalLineStarts.Count - 1;

        while (left <= right)
        {
            int middle = left + ((right - left) / 2);
            int value = _logicalLineStarts[middle];

            if (value == characterIndex)
            {
                logicalLineNumber = middle + 1;
                return true;
            }

            if (value < characterIndex)
            {
                left = middle + 1;
            }
            else
            {
                right = middle - 1;
            }
        }

        logicalLineNumber = 0;
        return false;
    }

    private static System.Windows.Rect GetCharacterRect(System.Windows.Controls.TextBox textBox, int characterIndex)
    {
        System.Windows.Rect rect = textBox.GetRectFromCharacterIndex(characterIndex);

        if (!rect.IsEmpty)
        {
            return rect;
        }

        rect = textBox.GetRectFromCharacterIndex(characterIndex, true);

        if (!rect.IsEmpty)
        {
            return rect;
        }

        if (characterIndex > 0)
        {
            rect = textBox.GetRectFromCharacterIndex(characterIndex - 1, true);
        }

        return rect;
    }
}
