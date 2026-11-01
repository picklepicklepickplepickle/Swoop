using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Swoop;

public sealed class SmoothProgressBar : ProgressBar
{
    public static readonly System.Windows.DependencyProperty TargetValueProperty = System.Windows.DependencyProperty.Register(
        nameof(TargetValue), typeof(double), typeof(SmoothProgressBar),
        new System.Windows.FrameworkPropertyMetadata(0d, OnTargetValueChanged));

    public double TargetValue
    {
        get => (double)GetValue(TargetValueProperty);
        set => SetValue(TargetValueProperty, value);
    }

    private static void OnTargetValueChanged(System.Windows.DependencyObject dependencyObject, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        var progressBar = (SmoothProgressBar)dependencyObject;
        var target = Math.Clamp((double)e.NewValue, progressBar.Minimum, progressBar.Maximum);
        var current = progressBar.Value;

        progressBar.BeginAnimation(ValueProperty, new DoubleAnimation
        {
            From = current,
            To = target,
            Duration = TimeSpan.FromMilliseconds(240),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        }, HandoffBehavior.SnapshotAndReplace);
    }
}
