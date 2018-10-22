using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;

namespace Carnassial.Dialog
{
    public partial class MessageBox : WindowWithSystemMenu
    {
        private MessageBoxButton buttonType;

        private MessageBox(Message message, Window owner, params object[] args)
        {
            this.InitializeComponent();
            this.ButtonType = message.Buttons;
            this.Message.Image = message.Image;
            this.Message.Title = message.Title;
            this.Message.ProblemText.Inlines.AddRange(this.Format(message.Problem, args));
            this.Message.WhatText.Inlines.AddRange(this.Format(message.What, args));
            this.Message.ReasonText.Inlines.AddRange(this.Format(message.Reason, args));
            this.Message.Solution.Inlines.AddRange(this.Format(message.Solution, args));
            this.Message.ResultText.Inlines.AddRange(this.Format(message.Result, args));
            this.Message.HintText.Inlines.AddRange(this.Format(message.Hint, args));
            this.Owner = owner;
            this.Title = message.WindowTitle;

            this.Message.SetVisibility();
        }

        public MessageBoxButton ButtonType
        {
            get
            {
                return this.buttonType;
            }
            set
            {
                switch (value)
                {
                    case MessageBoxButton.OK:
                        this.CancelButton.IsCancel = false;
                        this.CancelButton.Visibility = Visibility.Collapsed;
                        this.OKButton.Content = App.FindResource<string>(Constant.ResourceKey.DialogOK);
                        this.OKButton.IsCancel = true;
                        this.OKButton.Visibility = Visibility.Visible;
                        break;
                    case MessageBoxButton.OKCancel:
                        this.CancelButton.Content = App.FindResource<string>(Constant.ResourceKey.DialogCancel);
                        this.CancelButton.IsCancel = true;
                        this.CancelButton.Visibility = Visibility.Visible;
                        this.OKButton.Content = App.FindResource<string>(Constant.ResourceKey.DialogOK);
                        this.OKButton.IsCancel = false;
                        this.OKButton.Visibility = Visibility.Visible;
                        break;
                    case MessageBoxButton.YesNo:
                        this.CancelButton.Content = App.FindResource<string>(Constant.ResourceKey.DialogNo);
                        this.CancelButton.IsCancel = true;
                        this.CancelButton.Visibility = Visibility.Visible;
                        this.OKButton.Content = App.FindResource<string>(Constant.ResourceKey.DialogYes);
                        this.OKButton.IsCancel = false;
                        this.OKButton.Visibility = Visibility.Visible;
                        break;
                    case MessageBoxButton.YesNoCancel:
                    default:
                        throw new ArgumentOutOfRangeException(nameof(value), String.Format("Unhandled button type {0}.", value));
                }
                this.buttonType = value;
            }
        }

        public bool DisplayDontShowAgain
        {
            get
            {
                return this.DontShowAgain.Visibility == Visibility.Visible;
            }
            set
            {
                this.DontShowAgain.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private IEnumerable<object> Format(IEnumerable<Inline> textElements, object[] args)
        {
            if (textElements == null)
            {
                yield break;
            }

            foreach (TextElement textElement in textElements)
            {
                Debug.Assert(textElement != null, "Inline unexpectedly null.");
                if (textElement is LineBreak)
                {
                    yield return new LineBreak();
                }
                else if (textElement is Run run)
                {
                    yield return new Run(String.Format(run.Text, args));
                }
                else
                {
                    throw new NotSupportedException(String.Format("Unhandled inline of type {0}.", textElement.GetType()));
                }
            }
        }

        public static MessageBox FromResource(string key, Window owner, params object[] args)
        {
            return new MessageBox(App.FindResource<Message>(key), owner, args);
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
