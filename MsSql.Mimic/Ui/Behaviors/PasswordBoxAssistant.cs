using System.Windows;
using System.Windows.Controls;

namespace MsSql.Mimic.Ui.Behaviors;

/// <summary>
/// Provides attached dependency properties that enable two-way data binding for a WPF password box password value.
/// </summary>
/// <remarks>
/// This helper synchronizes an associated password box with a bindable string dependency property, allowing view models to receive password updates through standard WPF binding mechanisms. It is intended for scenarios where password entry must participate in binding while still using the native password box control.
/// </remarks>
public static class PasswordBoxAssistant
{
    /// <summary>
    /// Identifies the BoundPassword attached dependency property, which stores the password value synchronized with an associated PasswordBox for two-way binding.
    /// </summary>
    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BoundPassword",
            typeof(string),
            typeof(PasswordBoxAssistant),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnBoundPasswordChanged));

    /// <summary>
    /// Identifies the BindPassword attached dependency property, which enables or disables synchronization between a PasswordBox password value and the bound password dependency property.
    /// </summary>
    public static readonly DependencyProperty BindPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BindPassword",
            typeof(bool),
            typeof(PasswordBoxAssistant),
            new PropertyMetadata(false, OnBindPasswordChanged));

    /// <summary>
    /// Identifies the UpdatingPassword attached dependency property, which tracks whether password synchronization is currently in progress to prevent recursive updates.
    /// </summary>
    private static readonly DependencyProperty UpdatingPasswordProperty =
        DependencyProperty.RegisterAttached(
            "UpdatingPassword",
            typeof(bool),
            typeof(PasswordBoxAssistant),
            new PropertyMetadata(false));

    /// <summary>
    /// Gets the bound password value from the specified dependency object.
    /// </summary>
    /// <param name="dependencyObject">The dependency object from which to retrieve the bound password value.</param>
    /// <returns>The password value stored in the bound password attached property.</returns>
    public static string GetBoundPassword(DependencyObject dependencyObject)
    {
        return (string)dependencyObject.GetValue(BoundPasswordProperty);
    }

    /// <summary>
    /// Sets the bound password value on the specified dependency object.
    /// </summary>
    /// <param name="dependencyObject">The dependency object on which to set the bound password value.</param>
    /// <param name="value">The password value to store in the bound password attached property.</param>
    public static void SetBoundPassword(DependencyObject dependencyObject, string value)
    {
        dependencyObject.SetValue(BoundPasswordProperty, value);
    }

    public static bool GetBindPassword(DependencyObject dependencyObject)
    {
        return (bool)dependencyObject.GetValue(BindPasswordProperty);
    }

    public static void SetBindPassword(DependencyObject dependencyObject, bool value)
    {
        dependencyObject.SetValue(BindPasswordProperty, value);
    }

    private static bool GetUpdatingPassword(DependencyObject dependencyObject)
    {
        return (bool)dependencyObject.GetValue(UpdatingPasswordProperty);
    }

    private static void SetUpdatingPassword(DependencyObject dependencyObject, bool value)
    {
        dependencyObject.SetValue(UpdatingPasswordProperty, value);
    }

    private static void OnBindPasswordChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not PasswordBox passwordBox)
            return;

        if ((bool)e.OldValue)
            passwordBox.PasswordChanged -= OnPasswordChanged;

        if ((bool)e.NewValue)
            passwordBox.PasswordChanged += OnPasswordChanged;
    }

    /// <summary>
    /// Handles changes to the bound password value and updates the associated <see cref="PasswordBox"/> when needed.
    /// </summary>
    /// <param name="dependencyObject">The dependency object whose bound password value changed.</param>
    /// <param name="e">The event data containing the old and new bound password values.</param>
    private static void OnBoundPasswordChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not PasswordBox passwordBox)
            return;

        if (GetUpdatingPassword(passwordBox))
            return;

        var newPassword = e.NewValue as string ?? string.Empty;

        if (passwordBox.Password == newPassword)
            return;

        passwordBox.Password = newPassword;
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not PasswordBox passwordBox)
            return;

        SetUpdatingPassword(passwordBox, true);
        SetBoundPassword(passwordBox, passwordBox.Password);
        SetUpdatingPassword(passwordBox, false);
    }
}