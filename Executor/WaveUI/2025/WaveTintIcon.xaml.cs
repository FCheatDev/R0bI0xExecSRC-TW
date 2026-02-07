using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Executor.WaveUI
{
    public partial class WaveTintIcon : UserControl
    {
        public static readonly DependencyProperty IconNameProperty = DependencyProperty.Register(
            nameof(IconName),
            typeof(string),
            typeof(WaveTintIcon),
            new PropertyMetadata("", OnIconNameChanged));

        public static readonly DependencyProperty TintBrushProperty = DependencyProperty.Register(
            nameof(TintBrush),
            typeof(Brush),
            typeof(WaveTintIcon),
            new PropertyMetadata(Brushes.White));

        public WaveTintIcon()
        {
            InitializeComponent();
            Loaded += (_, _) => ApplyMask();
        }

        public string IconName
        {
            get => (string)GetValue(IconNameProperty);
            set => SetValue(IconNameProperty, value);
        }

        public Brush TintBrush
        {
            get => (Brush)GetValue(TintBrushProperty);
            set => SetValue(TintBrushProperty, value);
        }

        private static void OnIconNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not WaveTintIcon icon)
            {
                return;
            }

            icon.ApplyMask();
        }

        private void ApplyMask()
        {
            var img = WaveAssets.TryLoadIcon(IconName);
            if (img == null)
            {
                TintRect.OpacityMask = null;
                return;
            }

            var mask = new ImageBrush(img)
            {
                Stretch = Stretch.Uniform,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center,
            };
            mask.Freeze();
            TintRect.OpacityMask = mask;
        }
    }
}
