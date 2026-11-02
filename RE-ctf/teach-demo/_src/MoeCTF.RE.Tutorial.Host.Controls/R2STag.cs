using System.Windows;
using System.Windows.Controls;

namespace MoeCTF.RE.Tutorial.Host.Controls;

public sealed class R2STag : ContentControl
{
	public static readonly DependencyProperty LevelProperty = DependencyProperty.Register("Level", typeof(string), typeof(R2STag), new PropertyMetadata((object)"layer"));

	public string Level
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(LevelProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(LevelProperty, (object)value);
		}
	}
}
