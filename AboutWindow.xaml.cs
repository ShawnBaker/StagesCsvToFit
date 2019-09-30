// Copyright © 2019 Shawn Baker using the MIT License.
using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Navigation;

namespace StagesCsvToFit
{
	/// <summary>
	/// Interaction logic for AboutWindow.xaml
	/// </summary>
	public partial class AboutWindow : Window
	{
		/// <summary>
		/// Constructor - Initialize the controls.
		/// </summary>
		public AboutWindow()
		{
			InitializeComponent();

			// display the version number
			Assembly assembly = Assembly.GetExecutingAssembly();
			Version version = assembly.GetName().Version;
			string ver = string.Format("{0} {1}.{2}", Properties.Resources.Version, version.Major, version.Minor);
			if (version.Build != 0)
			{
				ver += "." + version.Build.ToString();
			}
			if (version.Revision != 0)
			{
				ver += "." + version.Revision.ToString();
			}
			VersionTextBlock.Text = ver;

			// display the copyright text
			CopyrightTextBlock.Text = ((AssemblyCopyrightAttribute)assembly.GetCustomAttribute(typeof(AssemblyCopyrightAttribute))).Copyright;

			// display the open source message with embedded links
			OpenSourceTextBlock.Inlines.Clear();
			OpenSourceTextBlock.Inlines.Add(new Run() { Text = Properties.Resources.OpenSource });
			OpenSourceTextBlock.Inlines.Add(new LineBreak());
			Hyperlink github = new Hyperlink() { NavigateUri = new Uri(@"https://github.com/ShawnBaker/StagesCsvToFit") };
			github.RequestNavigate += RequestNavigate;
			github.Inlines.Add(new Run() { Text = "github" });
			Hyperlink mit = new Hyperlink() { NavigateUri = new Uri(@"https://opensource.org/licenses/MIT") };
			mit.RequestNavigate += RequestNavigate;
			mit.Inlines.Add(new Run() { Text = "MIT" });
			string message = Properties.Resources.GithubMIT;
			int githubIndex = message.IndexOf("github");
			int mitIndex = message.IndexOf("MIT");
			bool mitFirst = mitIndex < githubIndex;
			int first = Math.Min(githubIndex, mitIndex);
			int second = Math.Max(githubIndex, mitIndex);
			int index = first;
			if (index != 0)
			{
				OpenSourceTextBlock.Inlines.Add(new Run() { Text = message.Substring(0, index) });
				index += mitFirst ? 3 : 6;
			}
			OpenSourceTextBlock.Inlines.Add(mitFirst ? mit : github);
			if (index != second)
			{
				int len = second - index;
				OpenSourceTextBlock.Inlines.Add(new Run() { Text = message.Substring(index, len) });
				index += len;
			}
			OpenSourceTextBlock.Inlines.Add(mitFirst ? github : mit);
			index += mitFirst ? 6 : 3;
			if (index < message.Length)
			{
				OpenSourceTextBlock.Inlines.Add(new Run() { Text = message.Substring(index) });
			}
		}

		/// <summary>
		/// Displays an embedded link in the default browser.
		/// </summary>
		private void RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			Process.Start(e.Uri.ToString());
		}

		/// <summary>
		/// Close the window.
		/// </summary>
		private void OKButton_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}
	}
}
