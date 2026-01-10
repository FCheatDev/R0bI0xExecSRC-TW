using System.Windows;

namespace Executor.WaveUI
{
    internal static class WaveToastService
    {
        private static WaveToastWindow? _window;

        internal static void Show(string title, string message)
        {
            if (Application.Current == null)
            {
                return;
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                // 如果視窗不存在，創建新視窗
                if (_window == null)
                {
                    _window = new WaveToastWindow();
                    _window.Closed += (_, _) => _window = null;
                }

                // ⚠️ 關鍵修復：不設置 Owner！
                // 因為視窗已經是 Topmost，設置 Owner 會導致衝突
                // 移除這段：
                // if (Application.Current.MainWindow != null && _window.Owner != Application.Current.MainWindow)
                // {
                //     _window.Owner = Application.Current.MainWindow;
                // }

                _window.ShowToast(title, message);
            });
        }
    }
}