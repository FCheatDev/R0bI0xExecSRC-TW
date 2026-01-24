using System;
using System.Windows;
using System.Windows.Input;

namespace Executor
{
    public partial class MultiInstanceWindow : Window
    {
        public MultiInstanceWindow()
        {
            InitializeComponent();
            ApplyLanguage();
            LocalizationManager.LanguageChanged += OnLanguageChanged;
            Closed += (_, _) => LocalizationManager.LanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged()
        {
            Dispatcher.BeginInvoke(new Action(ApplyLanguage));
        }

        private void ApplyLanguage()
        {
            if (TitleText != null)
            {
                TitleText.Text = LocalizationManager.T("WaveUI.MultiInstancePrompt.Title");
            }

            if (MessageText != null)
            {
                MessageText.Text = LocalizationManager.T("WaveUI.MultiInstancePrompt.Message");
            }

            if (ConfirmText != null)
            {
                ConfirmText.Text = LocalizationManager.T("WaveUI.MultiInstancePrompt.Confirm");
            }

            Title = TitleText?.Text ?? LocalizationManager.T("WaveUI.MultiInstancePrompt.Title");
        }

        private void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Border_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}
