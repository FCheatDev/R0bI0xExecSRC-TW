using System;
using System.Windows;
using System.Windows.Controls;

namespace Executor.WaveUI
{
    public sealed class WaveIcon : Image
    {
        internal static readonly DependencyProperty IconNameProperty = DependencyProperty.Register(
            nameof(IconName),
            typeof(string),
            typeof(WaveIcon),
            new PropertyMetadata("", OnIconNameChanged));

        public string IconName
        {
            get => (string)GetValue(IconNameProperty);
            set => SetValue(IconNameProperty, value);
        }

        private static void OnIconNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not WaveIcon icon)
            {
                return;
            }

            var name = e.NewValue as string;
            icon.Source = WaveAssets.TryLoadIcon(name ?? "");
        }
    }
}
