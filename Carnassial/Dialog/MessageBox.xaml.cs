﻿using Carnassial.Util;
using System;
using System.Windows;

namespace Carnassial.Dialog
{
    public partial class MessageBox : WindowWithSystemMenu
    {
        public MessageBox(string title, Window owner)
            : this(title, owner, MessageBoxButton.OK)
        {
        }

        public MessageBox(string title, Window owner, MessageBoxButton buttonType)
        {
            if (String.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("A title must be specified for the message box.", nameof(title));
            }

            this.InitializeComponent();
            this.Message.Title = title;
            this.Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.Title = title;

            switch (buttonType)
            {
                case MessageBoxButton.OK:
                    this.OkButton.IsCancel = true;
                    this.CancelButton.IsCancel = false;
                    this.CancelButton.IsEnabled = false;
                    break;
                case MessageBoxButton.OKCancel:
                    this.CancelButton.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.YesNo:
                    this.OkButton.Content = "_Yes";
                    this.CancelButton.Content = "_No";
                    this.CancelButton.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.YesNoCancel:
                default:
                    throw new ArgumentOutOfRangeException(nameof(buttonType), String.Format("Unhandled button type {0}.", buttonType));
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CommonUserInterface.SetDefaultDialogPosition(this);
            CommonUserInterface.TryFitWindowInWorkingArea(this);
        }
    }
}
