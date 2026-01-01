using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace DotGstm.Askpass;

public partial class AskpassWindow : Window
{
    private bool _passwordVisible = false;

    public AskpassWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 設定提示訊息（對應 gAskpass 的 argv[1]）
    /// </summary>
    public void SetPrompt(string prompt)
    {
        PromptText.Text = prompt;
    }

    /// <summary>
    /// 確定按鈕（對應 gAskpass callbacks.c:on_okbutton1_clicked）
    /// </summary>
    private void BtnOk_Click(object? sender, RoutedEventArgs e)
    {
        // 取得密碼文字
        var password = _passwordVisible ? PasswordTextBox.Text : PasswordBox.Text;

        // ⭐ 關鍵：輸出密碼到 stdout（對應 gAskpass callbacks.c:16 puts()）
        Console.Out.WriteLine(password ?? "");
        Console.Out.Flush();

        // 關閉視窗（對應 gAskpass callbacks.c:17 gtk_main_quit()）
        Close();
    }

    /// <summary>
    /// 取消按鈕（對應 gAskpass callbacks.c:on_cancelbutton1_clicked）
    /// </summary>
    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        // 取消時不輸出任何內容，直接關閉
        Close();
    }

    /// <summary>
    /// 顯示/隱藏密碼切換
    /// </summary>
    private void ShowPasswordToggle_Click(object? sender, RoutedEventArgs e)
    {
        _passwordVisible = !_passwordVisible;

        if (_passwordVisible)
        {
            // 顯示密碼：切換到 TextBox
            PasswordTextBox.Text = PasswordBox.Text;
            PasswordTextBox.IsVisible = true;
            PasswordBox.IsVisible = false;
            PasswordTextBox.Focus();
        }
        else
        {
            // 隱藏密碼：切換到 PasswordBox
            PasswordBox.Text = PasswordTextBox.Text;
            PasswordBox.IsVisible = true;
            PasswordTextBox.IsVisible = false;
            PasswordBox.Focus();
        }
    }
}
