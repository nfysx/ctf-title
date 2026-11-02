using System.Windows;
using System.Windows.Controls;

namespace MoeCTF.RE.Tutorial.Host.Controls;

public sealed class R2SCard : HeaderedContentControl
{
	public static readonly DependencyProperty LevelProperty = DependencyProperty.Register("Level", typeof(string), typeof(R2SCard), new PropertyMetadata((object)"default"));

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
