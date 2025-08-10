using System;
using System.Windows;
using System.Windows.Input;

namespace NA_ManagerShortcut.Views
{
    public partial class SaveProfileDialog : Window
    {
        public string ProfileName { get; private set; } = string.Empty;
        public string ProfileDescription { get; private set; } = string.Empty;

        public SaveProfileDialog()
        {
            InitializeComponent();
            ProfileNameBox.Text = $"Profile {DateTime.Now:yyyy-MM-dd HH:mm}";
            ProfileNameBox.Focus();
            ProfileNameBox.SelectAll();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ProfileNameBox.Text))
            {
                MessageBox.Show("Please enter a profile name.", "Validation", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ProfileName = ProfileNameBox.Text.Trim();
            ProfileDescription = ProfileDescriptionBox.Text.Trim();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}