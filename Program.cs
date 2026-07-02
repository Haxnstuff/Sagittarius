using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Markup;
using System.Windows.Data;
using System.Windows.Threading;
using System.Diagnostics;
using System.Reflection;

namespace Sagittarius
{
    public class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            App app = new App();
            app.Run();
        }
    }

    public class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            MainWindow mainWin = new MainWindow();
            mainWin.Show();
        }
    }

    public class FileItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Size { get; set; }
        public string Type { get; set; }
        public string Modified { get; set; }
        public bool IsDirectory { get; set; }
        public bool IsWpd { get; set; }
        public string Icon { get; set; }
        public string DisplayName { get; set; }
    }

    public class PinnedFolder
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public string Icon { get; set; }
    }

    public class SidebarItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Icon { get; set; }
        public bool IsWpd { get; set; }
        public bool IsHeader { get; set; }
        public bool IsDrive { get; set; }
        public double UsedPercentage { get; set; }
        public bool IsPinned { get; set; }

        public Visibility ProgressBarVisibility
        {
            get { return IsDrive ? Visibility.Visible : Visibility.Collapsed; }
        }

        public Visibility IconVisibility
        {
            get { return !IsHeader ? Visibility.Visible : Visibility.Collapsed; }
        }

        public Visibility HeaderCaretVisibility
        {
            get { return IsHeader ? Visibility.Visible : Visibility.Collapsed; }
        }

        public double ProgressBarValue
        {
            get { return UsedPercentage; }
        }

        public string ItemColor
        {
            get { return IsHeader ? "#606e88" : "#ff5500"; }
        }
        
        public string ItemFontWeight
        {
            get { return IsHeader ? "Bold" : "SemiBold"; }
        }
        
        public double ItemFontSize
        {
            get { return IsHeader ? 12 : 11.5; }
        }
        
        public Thickness ItemPadding
        {
            get { return IsHeader ? new Thickness(5, 10, 5, 2) : new Thickness(10, 4, 10, 4); }
        }
        
        public bool IsSelectable
        {
            get { return !IsHeader; }
        }
    }

    public class TabItem
    {
        public string Title { get; set; }
        public string CurrentPath { get; set; }
        public List<string> History { get; set; }
        public int HistoryIndex { get; set; }

        public TabItem(string title, string path)
        {
            History = new List<string>();
            HistoryIndex = -1;
            Title = title;
            CurrentPath = path;
            NavigateTo(path);
        }

        public void NavigateTo(string path)
        {
            // If we are navigating to a new path (not back/forward)
            if (HistoryIndex == -1 || History[HistoryIndex] != path)
            {
                // Truncate history after current index
                if (HistoryIndex < History.Count - 1 && HistoryIndex >= 0)
                {
                    History.RemoveRange(HistoryIndex + 1, History.Count - (HistoryIndex + 1));
                }
                History.Add(path);
                HistoryIndex = History.Count - 1;
            }
            CurrentPath = path;
            Title = System.IO.Path.GetFileName(path);
            if (string.IsNullOrEmpty(Title)) Title = path;
        }

        public bool CanGoBack
        {
            get { return HistoryIndex > 0; }
        }

        public bool CanGoForward
        {
            get { return HistoryIndex < History.Count - 1; }
        }

        public string GoBack()
        {
            if (CanGoBack)
            {
                HistoryIndex--;
                CurrentPath = History[HistoryIndex];
                Title = System.IO.Path.GetFileName(CurrentPath);
                if (string.IsNullOrEmpty(Title)) Title = CurrentPath;
            }
            return CurrentPath;
        }

        public string GoForward()
        {
            if (CanGoForward)
            {
                HistoryIndex++;
                CurrentPath = History[HistoryIndex];
                Title = System.IO.Path.GetFileName(CurrentPath);
                if (string.IsNullOrEmpty(Title)) Title = CurrentPath;
            }
            return CurrentPath;
        }
    }

    public class MainWindow : Window
    {
        private List<SidebarItem> sidebarItems;
        private List<TabItem> leftTabs;
        private List<TabItem> rightTabs;
        private List<PinnedFolder> pinnedFolders;
        private string pinnedFilePath;
        
        private List<TabItem> activeTabs
        {
            get { return isLeftPaneActive ? leftTabs : rightTabs; }
        }
        
        private int leftActiveTabIndex = 0;
        private int rightActiveTabIndex = 0;
        private int activeTabIndex
        {
            get { return isLeftPaneActive ? leftActiveTabIndex : rightActiveTabIndex; }
            set
            {
                if (isLeftPaneActive) leftActiveTabIndex = value;
                else rightActiveTabIndex = value;
            }
        }

        private TabItem activeTab
        {
            get { return activeTabs.Count > 0 ? activeTabs[activeTabIndex] : null; }
        }

        private bool isLeftPaneActive = true;
        private bool isSplit = true;

        // UI Controls
        private ListBox sidebarList;
        private ListBox leftTabHeaderList;
        private ListBox rightTabHeaderList;
        private ListView leftFileListView;
        private ListView rightFileListView;
        private TextBox leftPathTextBox;
        private TextBox rightPathTextBox;
        private Grid rightPaneGrid;
        private GridSplitter paneSplitter;
        private ContentControl inspectorPreview;
        private TextBlock inspectorFileName;
        private TextBlock inspectorFileInfo;
        private TextBlock statusBarText;
        private Grid commandPaletteOverlay;
        private TextBox commandPaletteSearch;
        private ListBox commandPaletteList;

        private Border leftPaneBorder;
        private Border rightPaneBorder;

        // Clipboard state
        private string clipboardSourcePath = null;
        private bool clipboardIsCut = false;

        // Cyberpunk colors
        private const string WindowXaml = @"
<Window xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        Title=""SAGITTARIUS // CYBERPUNK FILE SYSTEM"" Height=""750"" Width=""1250""
        WindowStyle=""None"" AllowsTransparency=""True"" Background=""Transparent""
        WindowStartupLocation=""CenterScreen"">
    
    <Window.Resources>
        <!-- Custom Scrollbar Styling -->
        <Style TargetType=""ScrollBar"">
            <Setter Property=""Background"" Value=""#0b0d13""/>
            <Setter Property=""Width"" Value=""8""/>
            <Setter Property=""Height"" Value=""8""/>
        </Style>
    </Window.Resources>

    <Border Background=""#090a0f"" BorderBrush=""#00f0ff"" BorderThickness=""1.5"" CornerRadius=""4"">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height=""35""/> <!-- Custom Title Bar -->
                <RowDefinition Height=""*""/> <!-- Main Workspace -->
                <RowDefinition Height=""25""/> <!-- Status Bar -->
            </Grid.RowDefinitions>

            <!-- Custom Title Bar -->
            <Grid Grid.Row=""0"" Background=""#0e111a"" Name=""TitleBarGrid"">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width=""*""/>
                    <ColumnDefinition Width=""Auto""/>
                </Grid.ColumnDefinitions>
                
                <StackPanel Orientation=""Horizontal"" VerticalAlignment=""Center"" Margin=""10,0,0,0"">
                    <TextBlock Text=""Sagittarius"" Foreground=""#00f0ff"" FontSize=""13"" FontWeight=""Bold"" VerticalAlignment=""Center""/>
                </StackPanel>

                <StackPanel Grid.Column=""1"" Orientation=""Horizontal"" Margin=""0,0,5,0"">
                    <Button Name=""BtnMin"" Content=""_"" Width=""30"" Height=""25"" Background=""Transparent"" BorderThickness=""0"" Foreground=""#00f0ff"" FontWeight=""Bold"" Cursor=""Hand""/>
                    <Button Name=""BtnMax"" Content=""[]"" Width=""30"" Height=""25"" Background=""Transparent"" BorderThickness=""0"" Foreground=""#00f0ff"" FontWeight=""Bold"" Cursor=""Hand""/>
                    <Button Name=""BtnClose"" Content=""X"" Width=""30"" Height=""25"" Background=""Transparent"" BorderThickness=""0"" Foreground=""#ff007f"" FontWeight=""Bold"" Cursor=""Hand""/>
                </StackPanel>
            </Grid>

            <!-- Main Workspace -->
            <Grid Grid.Row=""1"">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width=""220""/> <!-- Sidebar -->
                    <ColumnDefinition Width=""1.5""/> <!-- Sidebar Splitter -->
                    <ColumnDefinition Width=""*""/> <!-- File Explorer Panes -->
                    <ColumnDefinition Width=""1.5""/> <!-- Inspector Splitter -->
                    <ColumnDefinition Width=""260""/> <!-- Inspector Pane -->
                </Grid.ColumnDefinitions>

                <!-- Sidebar -->
                <Grid Grid.Column=""0"" Background=""#0a0c12"">
                    <ListBox Name=""SidebarList"" AllowDrop=""True"" Background=""Transparent"" BorderThickness=""0"" Margin=""5,10,5,5"" VirtualizingStackPanel.IsVirtualizing=""True"">
                        <ListBox.ItemContainerStyle>
                            <Style TargetType=""ListBoxItem"">
                                <Setter Property=""Background"" Value=""Transparent""/>
                                <Setter Property=""Foreground"" Value=""#c2cce0""/>
                                <Setter Property=""BorderThickness"" Value=""0""/>
                                <Setter Property=""Padding"" Value=""0""/>
                                <Setter Property=""Margin"" Value=""0""/>
                                <Setter Property=""Cursor"" Value=""Hand""/>
                                <Setter Property=""IsHitTestVisible"" Value=""{Binding IsSelectable}""/>
                                <Style.Triggers>
                                    <Trigger Property=""IsMouseOver"" Value=""True"">
                                        <Setter Property=""Background"" Value=""#1a2233""/>
                                    </Trigger>
                                    <Trigger Property=""IsSelected"" Value=""True"">
                                        <Setter Property=""Background"" Value=""#222e44""/>
                                    </Trigger>
                                </Style.Triggers>
                            </Style>
                        </ListBox.ItemContainerStyle>
                        <ListBox.ItemTemplate>
                            <DataTemplate>
                                <Grid Margin=""{Binding ItemPadding}"" HorizontalAlignment=""Stretch"">
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width=""Auto""/>
                                        <ColumnDefinition Width=""*""/>
                                        <ColumnDefinition Width=""Auto""/>
                                    </Grid.ColumnDefinitions>
                                    
                                    <TextBlock Grid.Column=""0"" Text=""{Binding Icon}"" Foreground=""#ff007f"" Margin=""0,0,10,0"" FontSize=""13"" VerticalAlignment=""Center"" Visibility=""{Binding IconVisibility}""/>
                                    
                                    <StackPanel Grid.Column=""1"" VerticalAlignment=""Center"" HorizontalAlignment=""Stretch"">
                                        <TextBlock Text=""{Binding Name}"" Foreground=""{Binding ItemColor}"" FontSize=""{Binding ItemFontSize}"" FontWeight=""{Binding ItemFontWeight}""/>
                                        <ProgressBar Value=""{Binding ProgressBarValue, Mode=OneWay}"" Maximum=""100"" Height=""3"" Background=""#1a2233"" BorderThickness=""0"" Margin=""0,4,0,0"" Foreground=""#00f0ff"" Visibility=""{Binding ProgressBarVisibility}""/>
                                    </StackPanel>
                                    
                                    <TextBlock Grid.Column=""2"" Text="""" Foreground=""#606e88"" FontSize=""10"" FontWeight=""Bold"" VerticalAlignment=""Center"" Visibility=""{Binding HeaderCaretVisibility}""/>
                                </Grid>
                            </DataTemplate>
                        </ListBox.ItemTemplate>
                    </ListBox>
                </Grid>

                <!-- Sidebar/Workspace Splitter -->
                <Border Grid.Column=""1"" Background=""#1a2233""/>

                <!-- File Explorer Panes -->
                <Grid Grid.Column=""2"" Background=""#090a0f"">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width=""*""/>
                        <ColumnDefinition Width=""Auto"" Name=""PaneSplitterCol""/>
                        <ColumnDefinition Width=""*"" Name=""RightPaneCol""/>
                    </Grid.ColumnDefinitions>

                    <!-- Left File Pane -->
                    <Border Grid.Column=""0"" Name=""LeftPaneBorder"" BorderBrush=""#00f0ff"" BorderThickness=""1"" Margin=""4"">
                        <Grid>
                            <Grid.RowDefinitions>
                                <RowDefinition Height=""32""/> <!-- Tabs -->
                                <RowDefinition Height=""35""/> <!-- Address Bar -->
                                <RowDefinition Height=""*""/> <!-- File List -->
                            </Grid.RowDefinitions>

                            <!-- Tabs -->
                            <Grid Grid.Row=""0"" Background=""#0e111a"">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width=""*""/>
                                    <ColumnDefinition Width=""Auto""/>
                                </Grid.ColumnDefinitions>
                                <ListBox Name=""LeftTabHeaderList"" Background=""Transparent"" BorderThickness=""0"" ScrollViewer.HorizontalScrollBarVisibility=""Disabled"" VerticalAlignment=""Center"">
                                    <ListBox.ItemsPanel>
                                        <ItemsPanelTemplate>
                                            <StackPanel Orientation=""Horizontal""/>
                                        </ItemsPanelTemplate>
                                    </ListBox.ItemsPanel>
                                    <ListBox.ItemContainerStyle>
                                        <Style TargetType=""ListBoxItem"">
                                            <Setter Property=""Background"" Value=""#131824""/>
                                            <Setter Property=""Foreground"" Value=""#8b9ebb""/>
                                            <Setter Property=""Padding"" Value=""10,4,10,4""/>
                                            <Setter Property=""Margin"" Value=""2,2,0,0""/>
                                            <Setter Property=""Cursor"" Value=""Hand""/>
                                            <Style.Triggers>
                                                <Trigger Property=""IsSelected"" Value=""True"">
                                                    <Setter Property=""Background"" Value=""#090a0f""/>
                                                    <Setter Property=""Foreground"" Value=""#00f0ff""/>
                                                    <Setter Property=""BorderBrush"" Value=""#00f0ff""/>
                                                    <Setter Property=""BorderThickness"" Value=""1,1,1,0""/>
                                                </Trigger>
                                                <Trigger Property=""IsMouseOver"" Value=""True"">
                                                    <Setter Property=""Foreground"" Value=""#00f0ff""/>
                                                </Trigger>
                                            </Style.Triggers>
                                        </Style>
                                    </ListBox.ItemContainerStyle>
                                    <ListBox.ItemTemplate>
                                        <DataTemplate>
                                            <StackPanel Orientation=""Horizontal"">
                                                <TextBlock Text=""{Binding Title}"" FontSize=""11"" FontWeight=""SemiBold"" Margin=""0,0,6,0""/>
                                                <Button Content=""x"" Background=""Transparent"" BorderThickness=""0"" Foreground=""#ff007f"" FontSize=""10"" FontWeight=""Bold"" VerticalAlignment=""Center"" Name=""BtnCloseLeftTab"" Padding=""2,0,2,0""/>
                                            </StackPanel>
                                        </DataTemplate>
                                    </ListBox.ItemTemplate>
                                </ListBox>
                                <Button Grid.Column=""1"" Name=""BtnAddLeftTab"" Content=""+"" Width=""25"" Height=""22"" Background=""#131824"" BorderBrush=""#1a2233"" Foreground=""#00f0ff"" FontWeight=""Bold"" Margin=""0,2,5,0"" Cursor=""Hand""/>
                            </Grid>

                            <!-- Address Bar -->
                            <Grid Grid.Row=""1"" Background=""#0a0d14"" SnapsToDevicePixels=""True"">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width=""Auto""/>
                                    <ColumnDefinition Width=""Auto""/>
                                    <ColumnDefinition Width=""Auto""/>
                                    <ColumnDefinition Width=""*""/>
                                </Grid.ColumnDefinitions>
                                <Button Name=""BtnLeftBack"" Content="" &lt; "" Height=""23"" Width=""25"" Background=""#131824"" BorderBrush=""#1a2233"" Foreground=""#00f0ff"" Margin=""4,0,2,0"" Cursor=""Hand""/>
                                <Button Grid.Column=""1"" Name=""BtnLeftForward"" Content="" &gt; "" Height=""23"" Width=""25"" Background=""#131824"" BorderBrush=""#1a2233"" Foreground=""#00f0ff"" Margin=""2,0,2,0"" Cursor=""Hand""/>
                                <Button Grid.Column=""2"" Name=""BtnLeftUp"" Content="" ^ "" Height=""23"" Width=""25"" Background=""#131824"" BorderBrush=""#1a2233"" Foreground=""#00f0ff"" Margin=""2,0,4,0"" Cursor=""Hand""/>
                                <TextBox Grid.Column=""3"" Name=""LeftPathTextBox"" Height=""23"" Background=""#090a0f"" BorderBrush=""#1a2233"" Foreground=""#39ff14"" CaretBrush=""#39ff14"" Padding=""5,2,5,2"" VerticalContentAlignment=""Center"" FontSize=""11.5"" Margin=""0,0,5,0""/>
                            </Grid>

                            <!-- File List -->
                            <ListView Grid.Row=""2"" Name=""LeftFileListView"" AllowDrop=""True"" Background=""#07080c"" BorderThickness=""0"" Foreground=""#c2cce0"" SelectionMode=""Extended"" KeyboardNavigation.DirectionalNavigation=""Contained"">
                                <ListView.ItemContainerStyle>
                                    <Style TargetType=""ListViewItem"">
                                        <Setter Property=""Background"" Value=""Transparent""/>
                                        <Setter Property=""Foreground"" Value=""#c2cce0""/>
                                        <Setter Property=""BorderThickness"" Value=""0""/>
                                        <Setter Property=""Margin"" Value=""0,1,0,1""/>
                                        <Setter Property=""Template"">
                                            <Setter.Value>
                                                <ControlTemplate TargetType=""ListViewItem"">
                                                    <Border Name=""ListItemBorder"" Background=""{TemplateBinding Background}"" BorderThickness=""0"" Padding=""0,3,0,3"">
                                                        <GridViewRowPresenter Content=""{TemplateBinding Content}"" Columns=""{TemplateBinding GridView.ColumnCollection}""/>
                                                    </Border>
                                                    <ControlTemplate.Triggers>
                                                        <Trigger Property=""IsMouseOver"" Value=""True"">
                                                            <Setter TargetName=""ListItemBorder"" Property=""Background"" Value=""#40e20055""/>
                                                            <Setter Property=""Foreground"" Value=""#00f0ff""/>
                                                        </Trigger>
                                                        <Trigger Property=""IsSelected"" Value=""True"">
                                                            <Setter TargetName=""ListItemBorder"" Property=""Background"" Value=""#60e20055""/>
                                                            <Setter Property=""Foreground"" Value=""#00f0ff""/>
                                                        </Trigger>
                                                    </ControlTemplate.Triggers>
                                                </ControlTemplate>
                                            </Setter.Value>
                                        </Setter>
                                    </Style>
                                </ListView.ItemContainerStyle>
                                <ListView.View>
                                    <GridView>
                                        <GridViewColumn Header=""Name"" Width=""220"">
                                            <GridViewColumn.CellTemplate>
                                                <DataTemplate>
                                                    <StackPanel Orientation=""Horizontal"">
                                                        <TextBlock Text=""{Binding Icon}"" Foreground=""#ff007f"" Margin=""0,0,6,0""/>
                                                        <TextBlock Text=""{Binding DisplayName}"" FontWeight=""SemiBold""/>
                                                    </StackPanel>
                                                </DataTemplate>
                                            </GridViewColumn.CellTemplate>
                                        </GridViewColumn>
                                        <GridViewColumn Header=""Size"" DisplayMemberBinding=""{Binding Size}"" Width=""70""/>
                                        <GridViewColumn Header=""Type"" DisplayMemberBinding=""{Binding Type}"" Width=""100""/>
                                        <GridViewColumn Header=""Date Modified"" DisplayMemberBinding=""{Binding Modified}"" Width=""120""/>
                                    </GridView>
                                </ListView.View>
                            </ListView>
                        </Grid>
                    </Border>

                    <!-- Splitter -->
                    <GridSplitter Grid.Column=""1"" Name=""PaneSplitter"" HorizontalAlignment=""Center"" VerticalAlignment=""Stretch"" Width=""3"" Background=""#1a2233"" ResizeBehavior=""PreviousAndNext""/>

                    <!-- Right File Pane -->
                    <Border Grid.Column=""2"" Name=""RightPaneBorder"" BorderBrush=""#1a2233"" BorderThickness=""1"" Margin=""4"">
                        <Grid Name=""RightPaneGrid"">
                            <Grid.RowDefinitions>
                                <RowDefinition Height=""32""/> <!-- Tabs -->
                                <RowDefinition Height=""35""/> <!-- Address Bar -->
                                <RowDefinition Height=""*""/> <!-- File List -->
                            </Grid.RowDefinitions>

                            <!-- Tabs -->
                            <Grid Grid.Row=""0"" Background=""#0e111a"">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width=""*""/>
                                    <ColumnDefinition Width=""Auto""/>
                                </Grid.ColumnDefinitions>
                                <ListBox Name=""RightTabHeaderList"" Background=""Transparent"" BorderThickness=""0"" ScrollViewer.HorizontalScrollBarVisibility=""Disabled"" VerticalAlignment=""Center"">
                                    <ListBox.ItemsPanel>
                                        <ItemsPanelTemplate>
                                            <StackPanel Orientation=""Horizontal""/>
                                        </ItemsPanelTemplate>
                                    </ListBox.ItemsPanel>
                                    <ListBox.ItemContainerStyle>
                                        <Style TargetType=""ListBoxItem"">
                                            <Setter Property=""Background"" Value=""#131824""/>
                                            <Setter Property=""Foreground"" Value=""#8b9ebb""/>
                                            <Setter Property=""Padding"" Value=""10,4,10,4""/>
                                            <Setter Property=""Margin"" Value=""2,2,0,0""/>
                                            <Setter Property=""Cursor"" Value=""Hand""/>
                                            <Style.Triggers>
                                                <Trigger Property=""IsSelected"" Value=""True"">
                                                    <Setter Property=""Background"" Value=""#090a0f""/>
                                                    <Setter Property=""Foreground"" Value=""#00f0ff""/>
                                                    <Setter Property=""BorderBrush"" Value=""#00f0ff""/>
                                                    <Setter Property=""BorderThickness"" Value=""1,1,1,0""/>
                                                </Trigger>
                                                <Trigger Property=""IsMouseOver"" Value=""True"">
                                                    <Setter Property=""Foreground"" Value=""#00f0ff""/>
                                                </Trigger>
                                            </Style.Triggers>
                                        </Style>
                                    </ListBox.ItemContainerStyle>
                                    <ListBox.ItemTemplate>
                                        <DataTemplate>
                                            <StackPanel Orientation=""Horizontal"">
                                                <TextBlock Text=""{Binding Title}"" FontSize=""11"" FontWeight=""SemiBold"" Margin=""0,0,6,0""/>
                                                <Button Content=""x"" Background=""Transparent"" BorderThickness=""0"" Foreground=""#ff007f"" FontSize=""10"" FontWeight=""Bold"" VerticalAlignment=""Center"" Name=""BtnCloseRightTab"" Padding=""2,0,2,0""/>
                                            </StackPanel>
                                        </DataTemplate>
                                    </ListBox.ItemTemplate>
                                </ListBox>
                                <Button Grid.Column=""1"" Name=""BtnAddRightTab"" Content=""+"" Width=""25"" Height=""22"" Background=""#131824"" BorderBrush=""#1a2233"" Foreground=""#00f0ff"" FontWeight=""Bold"" Margin=""0,2,5,0"" Cursor=""Hand""/>
                            </Grid>

                            <!-- Address Bar -->
                            <Grid Grid.Row=""1"" Background=""#0a0d14"" SnapsToDevicePixels=""True"">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width=""Auto""/>
                                    <ColumnDefinition Width=""Auto""/>
                                    <ColumnDefinition Width=""Auto""/>
                                    <ColumnDefinition Width=""*""/>
                                </Grid.ColumnDefinitions>
                                <Button Name=""BtnRightBack"" Content="" &lt; "" Height=""23"" Width=""25"" Background=""#131824"" BorderBrush=""#1a2233"" Foreground=""#00f0ff"" Margin=""4,0,2,0"" Cursor=""Hand""/>
                                <Button Grid.Column=""1"" Name=""BtnRightForward"" Content="" &gt; "" Height=""23"" Width=""25"" Background=""#131824"" BorderBrush=""#1a2233"" Foreground=""#00f0ff"" Margin=""2,0,2,0"" Cursor=""Hand""/>
                                <Button Grid.Column=""2"" Name=""BtnRightUp"" Content="" ^ "" Height=""23"" Width=""25"" Background=""#131824"" BorderBrush=""#1a2233"" Foreground=""#00f0ff"" Margin=""2,0,4,0"" Cursor=""Hand""/>
                                <TextBox Grid.Column=""3"" Name=""RightPathTextBox"" Height=""23"" Background=""#090a0f"" BorderBrush=""#1a2233"" Foreground=""#39ff14"" CaretBrush=""#39ff14"" Padding=""5,2,5,2"" VerticalContentAlignment=""Center"" FontSize=""11.5"" Margin=""0,0,5,0""/>
                            </Grid>

                            <!-- File List -->
                            <ListView Grid.Row=""2"" Name=""RightFileListView"" AllowDrop=""True"" Background=""#07080c"" BorderThickness=""0"" Foreground=""#c2cce0"" SelectionMode=""Extended"" KeyboardNavigation.DirectionalNavigation=""Contained"">
                                <ListView.ItemContainerStyle>
                                    <Style TargetType=""ListViewItem"">
                                        <Setter Property=""Background"" Value=""Transparent""/>
                                        <Setter Property=""Foreground"" Value=""#c2cce0""/>
                                        <Setter Property=""BorderThickness"" Value=""0""/>
                                        <Setter Property=""Margin"" Value=""0,1,0,1""/>
                                        <Setter Property=""Template"">
                                            <Setter.Value>
                                                <ControlTemplate TargetType=""ListViewItem"">
                                                    <Border Name=""ListItemBorder"" Background=""{TemplateBinding Background}"" BorderThickness=""0"" Padding=""0,3,0,3"">
                                                        <GridViewRowPresenter Content=""{TemplateBinding Content}"" Columns=""{TemplateBinding GridView.ColumnCollection}""/>
                                                    </Border>
                                                    <ControlTemplate.Triggers>
                                                        <Trigger Property=""IsMouseOver"" Value=""True"">
                                                            <Setter TargetName=""ListItemBorder"" Property=""Background"" Value=""#40e20055""/>
                                                            <Setter Property=""Foreground"" Value=""#00f0ff""/>
                                                        </Trigger>
                                                        <Trigger Property=""IsSelected"" Value=""True"">
                                                            <Setter TargetName=""ListItemBorder"" Property=""Background"" Value=""#60e20055""/>
                                                            <Setter Property=""Foreground"" Value=""#00f0ff""/>
                                                        </Trigger>
                                                    </ControlTemplate.Triggers>
                                                </ControlTemplate>
                                            </Setter.Value>
                                        </Setter>
                                    </Style>
                                </ListView.ItemContainerStyle>
                                <ListView.View>
                                    <GridView>
                                        <GridViewColumn Header=""Name"" Width=""220"">
                                            <GridViewColumn.CellTemplate>
                                                <DataTemplate>
                                                    <StackPanel Orientation=""Horizontal"">
                                                        <TextBlock Text=""{Binding Icon}"" Foreground=""#ff007f"" Margin=""0,0,6,0""/>
                                                        <TextBlock Text=""{Binding DisplayName}"" FontWeight=""SemiBold""/>
                                                    </StackPanel>
                                                </DataTemplate>
                                            </GridViewColumn.CellTemplate>
                                        </GridViewColumn>
                                        <GridViewColumn Header=""Size"" DisplayMemberBinding=""{Binding Size}"" Width=""70""/>
                                        <GridViewColumn Header=""Type"" DisplayMemberBinding=""{Binding Type}"" Width=""100""/>
                                        <GridViewColumn Header=""Date Modified"" DisplayMemberBinding=""{Binding Modified}"" Width=""120""/>
                                    </GridView>
                                </ListView.View>
                            </ListView>
                        </Grid>
                    </Border>
                </Grid>

                <!-- Workspace/Inspector Splitter -->
                <Border Grid.Column=""3"" Background=""#1a2233""/>

                <!-- Inspector Pane -->
                <Grid Grid.Column=""4"" Background=""#0a0c12"">
                    <Grid.RowDefinitions>
                        <RowDefinition Height=""Auto""/>
                        <RowDefinition Height=""Auto""/>
                        <RowDefinition Height=""*""/>
                    </Grid.RowDefinitions>

                    <TextBlock Grid.Row=""0"" Text=""PREVIEW INSPECTOR"" Foreground=""#00f0ff"" FontSize=""10"" FontWeight=""Bold"" Margin=""15,12,0,5""/>
                    
                    <StackPanel Grid.Row=""1"" Margin=""15,5,15,10"">
                        <TextBlock Name=""InspectorFileName"" Text=""No file selected"" Foreground=""#ff007f"" FontSize=""13"" FontWeight=""Bold"" TextTrimming=""CharacterEllipsis""/>
                        <TextBlock Name=""InspectorFileInfo"" Text=""Select a file to inspect metadata and contents."" Foreground=""#606e88"" FontSize=""11"" Margin=""0,4,0,0"" TextWrapping=""Wrap""/>
                    </StackPanel>

                    <!-- Preview Content -->
                    <Border Grid.Row=""2"" BorderBrush=""#1a2233"" BorderThickness=""1,0,0,0"" Margin=""10,0,10,10"">
                        <ContentControl Name=""InspectorPreview"" Background=""#07080c""/>
                    </Border>
                </Grid>
            </Grid>

            <!-- Status Bar -->
            <Grid Grid.Row=""2"" Background=""#0e111a"" SnapsToDevicePixels=""True"">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width=""*""/>
                    <ColumnDefinition Width=""Auto""/>
                </Grid.ColumnDefinitions>
                <TextBlock Name=""StatusBarText"" Text=""Ready // Sagittarius C.F.S initialized."" Foreground=""#606e88"" FontSize=""11"" VerticalAlignment=""Center"" Margin=""10,0,0,0""/>
                <TextBlock Grid.Column=""1"" Text=""[STA] // NEON ACTIVE "" Foreground=""#39ff14"" FontSize=""10"" FontWeight=""Bold"" VerticalAlignment=""Center"" Margin=""0,0,10,0""/>
            </Grid>

            <!-- Command Palette Overlay -->
            <Grid Name=""CommandPaletteOverlay"" Background=""#80000000"" Grid.Row=""0"" Grid.RowSpan=""3"" Visibility=""Collapsed"">
                <Border Background=""#0b0d13"" BorderBrush=""#ff007f"" BorderThickness=""2"" CornerRadius=""4"" Width=""450"" Height=""350"" HorizontalAlignment=""Center"" VerticalAlignment=""Center"">
                    <Grid Margin=""10"">
                        <Grid.RowDefinitions>
                            <RowDefinition Height=""35""/>
                            <RowDefinition Height=""*""/>
                            <RowDefinition Height=""25""/>
                        </Grid.RowDefinitions>

                        <!-- Search Box -->
                        <TextBox Name=""CommandPaletteSearch"" Background=""#07080c"" BorderBrush=""#00f0ff"" BorderThickness=""1"" Foreground=""#00f0ff"" CaretBrush=""#00f0ff"" Padding=""6,4,6,4"" FontSize=""12"" FontWeight=""SemiBold"" VerticalContentAlignment=""Center"" Grid.Row=""0""/>
                        
                        <!-- Command List -->
                        <ListBox Name=""CommandPaletteList"" Grid.Row=""1"" Margin=""0,10,0,10"" Background=""#07080c"" BorderThickness=""0"" Foreground=""#c2cce0"">
                            <ListBox.ItemContainerStyle>
                                <Style TargetType=""ListBoxItem"">
                                    <Setter Property=""Background"" Value=""Transparent""/>
                                    <Setter Property=""Padding"" Value=""8,4,8,4""/>
                                    <Setter Property=""Cursor"" Value=""Hand""/>
                                    <Style.Triggers>
                                        <Trigger Property=""IsMouseOver"" Value=""True"">
                                            <Setter Property=""Background"" Value=""#131824""/>
                                            <Setter Property=""Foreground"" Value=""#ff007f""/>
                                        </Trigger>
                                        <Trigger Property=""IsSelected"" Value=""True"">
                                            <Setter Property=""Background"" Value=""#1f293d""/>
                                            <Setter Property=""Foreground"" Value=""#00f0ff""/>
                                        </Trigger>
                                    </Style.Triggers>
                                </Style>
                            </ListBox.ItemContainerStyle>
                        </ListBox>

                        <TextBlock Grid.Row=""2"" Text=""Press ESC to close // Type command and press ENTER"" Foreground=""#606e88"" FontSize=""10"" HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
                    </Grid>
                </Border>
            </Grid>
        </Grid>
    </Border>
</Window>
";

        public MainWindow()
        {
            sidebarItems = new List<SidebarItem>();
            leftTabs = new List<TabItem>();
            rightTabs = new List<TabItem>();
            pinnedFolders = new List<PinnedFolder>();
            pinnedFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pinned_folders.txt");
            LoadPinnedFolders();

            // Set Window Startup properties
            this.AllowsTransparency = true;
            this.WindowStyle = WindowStyle.None;
            this.Background = Brushes.Transparent;

            // Parse and load dynamic XAML layout
            ParserContext pc = new ParserContext();
            pc.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
            pc.XmlnsDictionary.Add("x", "http://schemas.microsoft.com/winfx/2006/xaml");

            var windowContent = XamlReader.Parse(WindowXaml, pc);
            this.Content = ((Window)windowContent).Content;

            // Retrieve handles to UI elements
            BtnMin = (Button)FindNameInContent("BtnMin");
            BtnMax = (Button)FindNameInContent("BtnMax");
            BtnClose = (Button)FindNameInContent("BtnClose");
            sidebarList = (ListBox)FindNameInContent("SidebarList");
            leftTabHeaderList = (ListBox)FindNameInContent("LeftTabHeaderList");
            rightTabHeaderList = (ListBox)FindNameInContent("RightTabHeaderList");
            leftFileListView = (ListView)FindNameInContent("LeftFileListView");
            rightFileListView = (ListView)FindNameInContent("RightFileListView");
            leftPathTextBox = (TextBox)FindNameInContent("LeftPathTextBox");
            rightPathTextBox = (TextBox)FindNameInContent("RightPathTextBox");
            rightPaneGrid = (Grid)FindNameInContent("RightPaneGrid");
            paneSplitter = (GridSplitter)FindNameInContent("PaneSplitter");
            inspectorPreview = (ContentControl)FindNameInContent("InspectorPreview");
            inspectorFileName = (TextBlock)FindNameInContent("InspectorFileName");
            inspectorFileInfo = (TextBlock)FindNameInContent("InspectorFileInfo");
            statusBarText = (TextBlock)FindNameInContent("StatusBarText");
            commandPaletteOverlay = (Grid)FindNameInContent("CommandPaletteOverlay");
            commandPaletteSearch = (TextBox)FindNameInContent("CommandPaletteSearch");
            commandPaletteList = (ListBox)FindNameInContent("CommandPaletteList");
            leftPaneBorder = (Border)FindNameInContent("LeftPaneBorder");
            rightPaneBorder = (Border)FindNameInContent("RightPaneBorder");

            // Setup basic Window dragging
            var titleBar = (Grid)FindNameInContent("TitleBarGrid");
            titleBar.MouseDown += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    this.DragMove();
                }
            };

            // Hook up window controls
            BtnMin.Click += (s, e) => this.WindowState = WindowState.Minimized;
            BtnMax.Click += (s, e) => this.WindowState = (this.WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
            BtnClose.Click += (s, e) => this.Close();

            // Hook pane focus events to track active pane
            leftFileListView.GotFocus += (s, e) => SetActivePane(true);
            rightFileListView.GotFocus += (s, e) => SetActivePane(false);
            leftPathTextBox.GotFocus += (s, e) => SetActivePane(true);
            rightPathTextBox.GotFocus += (s, e) => SetActivePane(false);

            // Setup initial tabs
            leftTabs.Add(new TabItem("C:\\", "C:\\"));
            rightTabs.Add(new TabItem("D:\\", "D:\\"));

            leftTabHeaderList.ItemsSource = leftTabs;
            rightTabHeaderList.ItemsSource = rightTabs;
            leftTabHeaderList.SelectedIndex = 0;
            rightTabHeaderList.SelectedIndex = 0;

            // Initial UI update and load drives/sidebar
            RefreshSidebar();
            UpdatePaneUI();

            // Hook up ListBox/ListView event handlers
            sidebarList.SelectionChanged += SidebarList_SelectionChanged;
            sidebarList.MouseRightButtonUp += SidebarList_MouseRightButtonUp;
            sidebarList.PreviewMouseLeftButtonDown += SidebarList_PreviewMouseLeftButtonDown;
            sidebarList.MouseMove += SidebarList_MouseMove;
            sidebarList.DragOver += SidebarList_DragOver;
            sidebarList.Drop += SidebarList_Drop;
            leftTabHeaderList.SelectionChanged += (s, e) => {
                if (leftTabHeaderList.SelectedIndex >= 0)
                {
                    leftActiveTabIndex = leftTabHeaderList.SelectedIndex;
                    UpdatePaneUI();
                }
            };
            rightTabHeaderList.SelectionChanged += (s, e) => {
                if (rightTabHeaderList.SelectedIndex >= 0)
                {
                    rightActiveTabIndex = rightTabHeaderList.SelectedIndex;
                    UpdatePaneUI();
                }
            };

            leftFileListView.MouseDoubleClick += (s, e) => FileListView_DoubleClick(leftFileListView);
            rightFileListView.MouseDoubleClick += (s, e) => FileListView_DoubleClick(rightFileListView);

            leftFileListView.SelectionChanged += (s, e) => FileListView_SelectionChanged(leftFileListView);
            rightFileListView.SelectionChanged += (s, e) => FileListView_SelectionChanged(rightFileListView);

            leftFileListView.MouseRightButtonUp += (s, e) => ShowCustomContextMenu(leftFileListView);
            rightFileListView.MouseRightButtonUp += (s, e) => ShowCustomContextMenu(rightFileListView);

            leftFileListView.PreviewMouseLeftButtonDown += FileListView_PreviewMouseLeftButtonDown;
            leftFileListView.MouseMove += FileListView_MouseMove;
            leftFileListView.DragOver += FileListView_DragOver;
            leftFileListView.Drop += FileListView_Drop;

            rightFileListView.PreviewMouseLeftButtonDown += FileListView_PreviewMouseLeftButtonDown;
            rightFileListView.MouseMove += FileListView_MouseMove;
            rightFileListView.DragOver += FileListView_DragOver;
            rightFileListView.Drop += FileListView_Drop;

            // Hook up Tab buttons
            ((Button)FindNameInContent("BtnAddLeftTab")).Click += (s, e) => AddTab(true);
            ((Button)FindNameInContent("BtnAddRightTab")).Click += (s, e) => AddTab(false);

            // Hook up Back, Forward, Up buttons
            ((Button)FindNameInContent("BtnLeftBack")).Click += (s, e) => GoBack(true);
            ((Button)FindNameInContent("BtnRightBack")).Click += (s, e) => GoBack(false);
            ((Button)FindNameInContent("BtnLeftForward")).Click += (s, e) => GoForward(true);
            ((Button)FindNameInContent("BtnRightForward")).Click += (s, e) => GoForward(false);
            ((Button)FindNameInContent("BtnLeftUp")).Click += (s, e) => GoUp(true);
            ((Button)FindNameInContent("BtnRightUp")).Click += (s, e) => GoUp(false);

            // Hook up close tab routed events from the data templates
            leftTabHeaderList.AddHandler(Button.ClickEvent, new RoutedEventHandler(LeftTabClickHeaderHandler));
            rightTabHeaderList.AddHandler(Button.ClickEvent, new RoutedEventHandler(RightTabClickHeaderHandler));

            // Key Handlers for path typing
            leftPathTextBox.KeyDown += PathTextBox_KeyDown;
            rightPathTextBox.KeyDown += PathTextBox_KeyDown;

            // Global Hotkeys
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;

            // Command Palette Search text changes
            commandPaletteSearch.TextChanged += (s, e) => FilterPaletteCommands();

            // Command Palette List Item double click
            commandPaletteList.MouseDoubleClick += (s, e) => ExecutePaletteCommand();
            commandPaletteSearch.KeyDown += (s, e) => {
                if (e.Key == Key.Enter) ExecutePaletteCommand();
                else if (e.Key == Key.Escape) ToggleCommandPalette();
                else if (e.Key == Key.Down)
                {
                    commandPaletteList.Focus();
                    if (commandPaletteList.Items.Count > 0 && commandPaletteList.SelectedIndex == -1)
                        commandPaletteList.SelectedIndex = 0;
                }
            };
            commandPaletteList.KeyDown += (s, e) => {
                if (e.Key == Key.Enter) ExecutePaletteCommand();
                else if (e.Key == Key.Escape) ToggleCommandPalette();
            };

            // Periodically poll for newly connected WPD devices (plugged in microcontrollers)
            DispatcherTimer driveMonitorTimer = new DispatcherTimer();
            driveMonitorTimer.Interval = TimeSpan.FromSeconds(3.5);
            driveMonitorTimer.Tick += (s, e) => RefreshWpdDevicesOnly();
            driveMonitorTimer.Start();
        }

        private const int WM_NCHITTEST = 0x0084;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var source = System.Windows.Interop.HwndSource.FromVisual(this) as System.Windows.Interop.HwndSource;
            if (source != null)
            {
                source.AddHook(new System.Windows.Interop.HwndSourceHook(WndProc));
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_NCHITTEST)
            {
                short x = (short)(lParam.ToInt32() & 0xffff);
                short y = (short)((lParam.ToInt32() >> 16) & 0xffff);
                
                try
                {
                    var wp = this.PointFromScreen(new Point(x, y));
                    
                    double thickness = 7;
                    bool left = wp.X <= thickness;
                    bool right = wp.X >= this.ActualWidth - thickness;
                    bool top = wp.Y <= thickness;
                    bool bottom = wp.Y >= this.ActualHeight - thickness;

                    if (top && left) { handled = true; return new IntPtr(HTTOPLEFT); }
                    if (top && right) { handled = true; return new IntPtr(HTTOPRIGHT); }
                    if (bottom && left) { handled = true; return new IntPtr(HTBOTTOMLEFT); }
                    if (bottom && right) { handled = true; return new IntPtr(HTBOTTOMRIGHT); }
                    if (left) { handled = true; return new IntPtr(HTLEFT); }
                    if (right) { handled = true; return new IntPtr(HTRIGHT); }
                    if (top) { handled = true; return new IntPtr(HTTOP); }
                    if (bottom) { handled = true; return new IntPtr(HTBOTTOM); }
                }
                catch { }
            }
            return IntPtr.Zero;
        }

        private void LeftTabClickHeaderHandler(object sender, RoutedEventArgs e)
        {
            var button = e.OriginalSource as Button;
            if (button != null && (button.Name == "BtnCloseLeftTab" || button.Content.ToString() == "x"))
            {
                var tab = button.DataContext as TabItem;
                if (tab != null)
                {
                    int index = leftTabs.IndexOf(tab);
                    if (index >= 0)
                    {
                        CloseTab(true, index);
                    }
                }
                e.Handled = true;
            }
        }

        private void RightTabClickHeaderHandler(object sender, RoutedEventArgs e)
        {
            var button = e.OriginalSource as Button;
            if (button != null && (button.Name == "BtnCloseRightTab" || button.Content.ToString() == "x"))
            {
                var tab = button.DataContext as TabItem;
                if (tab != null)
                {
                    int index = rightTabs.IndexOf(tab);
                    if (index >= 0)
                    {
                        CloseTab(false, index);
                    }
                }
                e.Handled = true;
            }
        }

        private object FindNameInContent(string name)
        {
            var contentElement = this.Content as FrameworkElement;
            if (contentElement != null)
            {
                object obj = contentElement.FindName(name);
                if (obj != null) return obj;
                // Fallback to recursive logical tree search
                var depObj = FindLogicalChild(contentElement, name);
                if (depObj != null) return depObj;
            }
            return null;
        }

        private DependencyObject FindLogicalChild(DependencyObject parent, string name)
        {
            if (parent == null) return null;
            var frameworkEl = parent as FrameworkElement;
            if (frameworkEl != null && frameworkEl.Name == name) return parent;

            foreach (var child in LogicalTreeHelper.GetChildren(parent))
            {
                var depChild = child as DependencyObject;
                if (depChild == null) continue;
                var found = FindLogicalChild(depChild, name);
                if (found != null) return found;
            }
            return null;
        }

        private Button BtnMin, BtnMax, BtnClose;

        private void SetActivePane(bool isLeft)
        {
            isLeftPaneActive = isLeft;
            if (isLeft)
            {
                leftPaneBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00f0ff"));
                rightPaneBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1a2233"));
            }
            else
            {
                rightPaneBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00f0ff"));
                leftPaneBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1a2233"));
            }
        }

        private void SetStatus(string text)
        {
            statusBarText.Text = "Ready // " + text;
        }

        private void RefreshSidebar()
        {
            sidebarItems.Clear();

            if (pinnedFolders != null)
            {
                foreach (var pf in pinnedFolders)
                {
                    sidebarItems.Add(new SidebarItem 
                    { 
                        Name = pf.Name, 
                        Path = pf.Path, 
                        Icon = pf.Icon, 
                        IsWpd = pf.Path.StartsWith("::"), 
                        IsHeader = false, 
                        IsDrive = false, 
                        IsPinned = true 
                    });
                }
            }

            // Storage Header
            sidebarItems.Add(new SidebarItem { Name = "Storage", Path = "", Icon = "", IsWpd = false, IsHeader = true, IsDrive = false });

            // Drives with capacity calculations
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    string label = string.IsNullOrEmpty(drive.VolumeLabel) ? "Local Disk" : drive.VolumeLabel;
                    double usedSpace = drive.TotalSize - drive.TotalFreeSpace;
                    double usedPct = 0;
                    if (drive.TotalSize > 0)
                    {
                        usedPct = (usedSpace / drive.TotalSize) * 100.0;
                    }

                    sidebarItems.Add(new SidebarItem
                    {
                        Name = string.Format("{0} ({1})", label, drive.Name.TrimEnd('\\')),
                        Path = drive.Name,
                        Icon = "Drive",
                        IsWpd = false,
                        IsHeader = false,
                        IsDrive = true,
                        UsedPercentage = usedPct
                    });
                }
            }

            // Find WPD microcontrollers / USB devices
            try
            {
                Type shellType = Type.GetTypeFromProgID("Shell.Application");
                object shellObj = System.Activator.CreateInstance(shellType);
                dynamic shell = shellObj;
                dynamic thisPC = shell.NameSpace("shell:MyComputerFolder");
                if (thisPC != null)
                {
                    foreach (dynamic item in thisPC.Items())
                    {
                        string path = item.Path;
                        string name = item.Name;
                        if (path.StartsWith("::") && (path.Contains("usb#") || path.Contains("VID_") || name == "USB of Babel" || path.Contains("wpdbus")))
                        {
                            sidebarItems.Add(new SidebarItem
                            {
                                Name = string.Format("{0} (Device)", name),
                                Path = path,
                                Icon = "Device",
                                IsWpd = true,
                                IsHeader = false,
                                IsDrive = false
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("WPD discovery error: " + ex.Message);
            }

            sidebarList.ItemsSource = null;
            sidebarList.ItemsSource = sidebarItems;
        }

        private void RefreshWpdDevicesOnly()
        {
            var newWpdDevices = new List<SidebarItem>();
            try
            {
                Type shellType = Type.GetTypeFromProgID("Shell.Application");
                object shell = System.Activator.CreateInstance(shellType);
                dynamic thisPC = ((dynamic)shell).NameSpace("shell:MyComputerFolder");
                if (thisPC != null)
                {
                    foreach (dynamic item in thisPC.Items())
                    {
                        string path = item.Path;
                        string name = item.Name;
                        if (path.StartsWith("::") && (path.Contains("usb#") || path.Contains("VID_") || name == "USB of Babel" || path.Contains("wpdbus")))
                        {
                            newWpdDevices.Add(new SidebarItem
                            {
                                Name = string.Format("{0} (Device)", name),
                                Path = path,
                                Icon = "Device",
                                IsWpd = true,
                                IsHeader = false,
                                IsDrive = false
                            });
                        }
                    }
                }
            }
            catch { }

            var existingWpd = sidebarItems.Where(i => i.IsWpd && !i.IsPinned).ToList();
            bool changed = (existingWpd.Count != newWpdDevices.Count);
            if (!changed)
            {
                for (int i = 0; i < existingWpd.Count; i++)
                {
                    if (existingWpd[i].Path != newWpdDevices[i].Path || existingWpd[i].Name != newWpdDevices[i].Name)
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (changed)
            {
                RefreshSidebar();
                SetStatus("Hardware configuration changed. Devices updated.");
            }
        }

        private void AddTab(bool isLeft)
        {
            var tabs = isLeft ? leftTabs : rightTabs;
            string defaultPath = "C:\\";
            tabs.Add(new TabItem("Local Disk (C:)", defaultPath));
            
            if (isLeft)
            {
                leftTabHeaderList.ItemsSource = null;
                leftTabHeaderList.ItemsSource = leftTabs;
                leftTabHeaderList.SelectedIndex = leftTabs.Count - 1;
            }
            else
            {
                rightTabHeaderList.ItemsSource = null;
                rightTabHeaderList.ItemsSource = rightTabs;
                rightTabHeaderList.SelectedIndex = rightTabs.Count - 1;
            }
            UpdatePaneUI();
        }

        private void CloseTab(bool isLeft, int index)
        {
            var tabs = isLeft ? leftTabs : rightTabs;
            if (tabs.Count <= 1) return; // Must have at least one tab

            tabs.RemoveAt(index);
            int newIndex = index == 0 ? 0 : index - 1;

            if (isLeft)
            {
                leftTabHeaderList.ItemsSource = null;
                leftTabHeaderList.ItemsSource = leftTabs;
                leftTabHeaderList.SelectedIndex = newIndex;
            }
            else
            {
                rightTabHeaderList.ItemsSource = null;
                rightTabHeaderList.ItemsSource = rightTabs;
                rightTabHeaderList.SelectedIndex = newIndex;
            }
            UpdatePaneUI();
        }

        private void GoBack(bool isLeft)
        {
            var tab = isLeft ? (leftTabs.Count > 0 ? leftTabs[leftActiveTabIndex] : null) : (rightTabs.Count > 0 ? rightTabs[rightActiveTabIndex] : null);
            if (tab != null && tab.CanGoBack)
            {
                tab.GoBack();
                UpdatePaneUI();
            }
        }

        private void GoForward(bool isLeft)
        {
            var tab = isLeft ? (leftTabs.Count > 0 ? leftTabs[leftActiveTabIndex] : null) : (rightTabs.Count > 0 ? rightTabs[rightActiveTabIndex] : null);
            if (tab != null && tab.CanGoForward)
            {
                tab.GoForward();
                UpdatePaneUI();
            }
        }

        private void GoUp(bool isLeft)
        {
            var tab = isLeft ? (leftTabs.Count > 0 ? leftTabs[leftActiveTabIndex] : null) : (rightTabs.Count > 0 ? rightTabs[rightActiveTabIndex] : null);
            if (tab == null) return;

            string path = tab.CurrentPath;
            if (path.StartsWith("::"))
            {
                // WPD device hierarchy navigation
                if (path.Contains("\\"))
                {
                    int lastBackslash = path.LastIndexOf('\\');
                    string parentPath = path.Substring(0, lastBackslash);
                    tab.NavigateTo(parentPath);
                    UpdatePaneUI();
                }
                else
                {
                    // Navigate to "This PC"
                    tab.NavigateTo("C:\\");
                    UpdatePaneUI();
                }
            }
            else
            {
                // Local directory navigation
                var parent = Directory.GetParent(path);
                if (parent != null)
                {
                    tab.NavigateTo(parent.FullName);
                    UpdatePaneUI();
                }
            }
        }

        private void UpdatePaneUI()
        {
            // Refreshes the active tabs, file items, and paths on both panels
            if (leftTabs.Count > 0 && leftActiveTabIndex < leftTabs.Count)
            {
                var tab = leftTabs[leftActiveTabIndex];
                leftPathTextBox.Text = FormatDisplayPath(tab.CurrentPath);
                leftFileListView.ItemsSource = GetDirectoryContents(tab.CurrentPath);
            }

            if (rightTabs.Count > 0 && rightActiveTabIndex < rightTabs.Count)
            {
                var tab = rightTabs[rightActiveTabIndex];
                rightPathTextBox.Text = FormatDisplayPath(tab.CurrentPath);
                rightFileListView.ItemsSource = GetDirectoryContents(tab.CurrentPath);
            }
        }

        private dynamic ResolveWpdFolder(string fullPath)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("Shell.Application");
                object shellObj = System.Activator.CreateInstance(shellType);
                dynamic shell = shellObj;

                int firstBrace = fullPath.IndexOf('{');
                if (firstBrace < 0) return null;
                int firstClose = fullPath.IndexOf('}', firstBrace);
                if (firstClose < 0) return null;

                int guidEnd = firstClose;

                int secondBrace = fullPath.IndexOf('{', firstClose + 1);
                if (secondBrace >= 0)
                {
                    int secondClose = fullPath.IndexOf('}', secondBrace);
                    if (secondClose >= 0)
                    {
                        guidEnd = secondClose;
                    }
                }

                int rootEnd = fullPath.IndexOf('\\', guidEnd);
                string rootPath;
                string subPath = "";
                if (rootEnd < 0)
                {
                    rootPath = fullPath;
                }
                else
                {
                    rootPath = fullPath.Substring(0, rootEnd);
                    subPath = fullPath.Substring(rootEnd + 1);
                }

                dynamic currentFolder = shell.NameSpace(rootPath);
                if (currentFolder == null) return null;

                if (string.IsNullOrEmpty(subPath))
                {
                    return currentFolder;
                }

                string[] segments = subPath.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var segment in segments)
                {
                    dynamic foundItem = null;
                    foreach (dynamic item in currentFolder.Items())
                    {
                        string itemName = item.Name;
                        string itemPath = item.Path;
                        string lastPart = System.IO.Path.GetFileName(itemPath);
                        if (string.IsNullOrEmpty(lastPart))
                        {
                            int lastBS = itemPath.LastIndexOf('\\');
                            if (lastBS >= 0) lastPart = itemPath.Substring(lastBS + 1);
                            else lastPart = itemPath;
                        }

                        if (string.Equals(itemName, segment, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(lastPart, segment, StringComparison.OrdinalIgnoreCase))
                        {
                            foundItem = item;
                            break;
                        }
                    }

                    if (foundItem == null) return null;
                    if (!foundItem.IsFolder) return null;

                    currentFolder = foundItem.GetFolder;
                    if (currentFolder == null) return null;
                }

                return currentFolder;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ResolveWpdFolder failed: " + ex.Message);
                return null;
            }
        }

        private string FormatDisplayPath(string rawPath)
        {
            if (rawPath.StartsWith("::"))
            {
                // Fallback parsing for USB of Babel specifically
                if (rawPath.Contains("usb#vid_303a&pid_4020#c41e7ad4db1c#{6ac27878-a6fa-4155-ba85-f98f491d4f33}"))
                {
                    string rest = rawPath.Substring(rawPath.IndexOf('}') + 1).TrimStart('\\');
                    if (rest.StartsWith("SID-{10001,vol,0}"))
                    {
                        rest = "disk" + rest.Substring("SID-{10001,vol,0}".Length);
                    }
                    return "USB of Babel:\\" + rest;
                }
                
                int lastBrace = rawPath.LastIndexOf('}');
                if (lastBrace >= 0 && lastBrace < rawPath.Length - 1)
                {
                    string rest = rawPath.Substring(lastBrace + 1).TrimStart('\\');
                    return "Device:\\" + rest;
                }
                return "Device:\\";
            }
            return rawPath;
        }

        private List<FileItem> GetDirectoryContents(string path)
        {
            List<FileItem> items = new List<FileItem>();

            if (path.StartsWith("::"))
            {
                // WPD device listing
                try
                {
                    dynamic folder = ResolveWpdFolder(path);
                    if (folder != null)
                    {
                        foreach (dynamic item in folder.Items())
                        {
                            bool isDir = item.IsFolder;
                            string name = item.Name;
                            string itemPath = item.Path;
                            string sizeStr = folder.GetDetailsOf(item, 2); // Size
                            string typeStr = folder.GetDetailsOf(item, 1); // Type
                            string modifiedStr = folder.GetDetailsOf(item, 3); // Date modified

                            items.Add(new FileItem
                            {
                                Name = name,
                                DisplayName = name,
                                Path = itemPath,
                                Size = isDir ? "DIR" : (string.IsNullOrEmpty(sizeStr) ? "0 KB" : sizeStr),
                                Type = string.IsNullOrEmpty(typeStr) ? (isDir ? "Folder" : "File") : typeStr,
                                Modified = modifiedStr,
                                IsDirectory = isDir,
                                IsWpd = true,
                                Icon = isDir ? "Folder" : "File"
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    SetStatus("Error reading device namespace: " + ex.Message);
                }
            }
            else
            {
                // Standard Local Directory
                try
                {
                    if (Directory.Exists(path))
                    {
                        foreach (var dir in Directory.GetDirectories(path))
                        {
                            var info = new DirectoryInfo(dir);
                            if ((info.Attributes & FileAttributes.Hidden) != 0 && !ShowHiddenFiles) continue;
                            
                            items.Add(new FileItem
                            {
                                Name = info.Name,
                                DisplayName = info.Name,
                                Path = info.FullName,
                                Size = "DIR",
                                Type = "File folder",
                                Modified = info.LastWriteTime.ToString(),
                                IsDirectory = true,
                                IsWpd = false,
                                Icon = "Folder"
                            });
                        }

                        foreach (var file in Directory.GetFiles(path))
                        {
                            var info = new FileInfo(file);
                            if ((info.Attributes & FileAttributes.Hidden) != 0 && !ShowHiddenFiles) continue;

                            items.Add(new FileItem
                            {
                                Name = info.Name,
                                DisplayName = info.Name,
                                Path = info.FullName,
                                Size = FormatSize(info.Length),
                                Type = GetFileTypeString(info.Extension),
                                Modified = info.LastWriteTime.ToString(),
                                IsDirectory = false,
                                IsWpd = false,
                                Icon = "File"
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    SetStatus("Access denied: " + ex.Message);
                }
            }

            return items;
        }

        private bool ShowHiddenFiles = true;

        private string FormatSize(long bytes)
        {
            if (bytes >= 1073741824) return string.Format("{0:F1} GB", bytes / 1073741824.0);
            if (bytes >= 1048576) return string.Format("{0:F1} MB", bytes / 1048576.0);
            if (bytes >= 1024) return string.Format("{0:F1} KB", bytes / 1024.0);
            return bytes.ToString() + " B";
        }

        private string GetFileTypeString(string ext)
        {
            if (string.IsNullOrEmpty(ext)) return "File";
            ext = ext.ToLower();
            if (ext == ".txt") return "Text Document";
            if (ext == ".json") return "JSON File";
            if (ext == ".exe") return "Application";
            if (ext == ".dll") return "System Library";
            if (ext == ".zip") return "Compressed Archive";
            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp") return "Image File";
            if (ext == ".mid" || ext == ".mp3" || ext == ".wav") return "Audio File";
            return ext.Substring(1).ToUpper() + " File";
        }

        private void SidebarList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = sidebarList.SelectedItem as SidebarItem;
            if (selected != null)
            {
                if (selected.IsHeader)
                {
                    sidebarList.SelectedItem = null;
                    return;
                }
                if (activeTab != null)
                {
                    activeTab.NavigateTo(selected.Path);
                    UpdatePaneUI();
                    SetActivePane(isLeftPaneActive); // ensure active highlighting remains
                }
            }
        }

        private void FileListView_DoubleClick(ListView fileListView)
        {
            var selected = fileListView.SelectedItem as FileItem;
            if (selected == null || activeTab == null) return;

            if (selected.IsDirectory)
            {
                activeTab.NavigateTo(selected.Path);
                UpdatePaneUI();
            }
            else
            {
                // Open file natively
                try
                {
                    if (selected.IsWpd)
                    {
                        // Microcontrollers/WPD devices: copy to temp and open
                        string tempPath = Path.Combine(Path.GetTempPath(), selected.Name);
                        SetStatus("Downloading file from microcontroller...");
                        
                        ThreadPool.QueueUserWorkItem((state) => {
                            bool success = CopyWpdFileToLocal(selected.Path, tempPath);
                            Dispatcher.Invoke(() => {
                                if (success && File.Exists(tempPath))
                                {
                                    Process.Start(tempPath);
                                    SetStatus("File loaded from microcontroller.");
                                }
                                else
                                {
                                    MessageBox.Show("Failed to download file from device.");
                                    SetStatus("Download failed.");
                                }
                            });
                        });
                    }
                    else
                    {
                        Process.Start(selected.Path);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not open file: " + ex.Message);
                }
            }
        }

        private void FileListView_SelectionChanged(ListView fileListView)
        {
            var selected = fileListView.SelectedItem as FileItem;
            if (selected == null)
            {
                inspectorFileName.Text = "No file selected";
                inspectorFileInfo.Text = "Select a file to inspect metadata and contents.";
                inspectorPreview.Content = null;
                return;
            }

            inspectorFileName.Text = selected.Name;
            inspectorFileInfo.Text = string.Format("Type: {0}\nSize: {1}\nModified: {2}", selected.Type, selected.Size, selected.Modified);

            if (selected.IsDirectory)
            {
                inspectorPreview.Content = new TextBlock
                {
                    Text = "Folder preview not available.",
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(10)
                };
                return;
            }

            // Preview Text and Image files
            string ext = System.IO.Path.GetExtension(selected.Name).ToLower();
            bool isText = new[] { ".txt", ".json", ".c", ".h", ".cpp", ".js", ".md", ".ini", ".bat", ".xml", ".css" }.Contains(ext);
            bool isImage = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif" }.Contains(ext);

            if (selected.IsWpd)
            {
                // Preview for WPD files requires downloading to temp folder
                inspectorPreview.Content = new TextBlock { Text = "Loading preview...", Foreground = Brushes.Gray, Margin = new Thickness(10) };
                
                ThreadPool.QueueUserWorkItem((state) => {
                    string tempFile = Path.Combine(Path.GetTempPath(), "sagittarius_prev_" + selected.Name);
                    bool success = CopyWpdFileToLocal(selected.Path, tempFile);
                    
                    Dispatcher.Invoke(() => {
                        if (success && File.Exists(tempFile))
                        {
                            try
                            {
                                if (isText)
                                {
                                    string text = File.ReadAllText(tempFile);
                                    if (text.Length > 20000) text = text.Substring(0, 20000) + "\r\n... [TRUNCATED] ...";
                                    var textBlock = new TextBox
                                    {
                                        Text = text,
                                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#07080c")),
                                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e0e0e0")),
                                        BorderThickness = new Thickness(0),
                                        IsReadOnly = true,
                                        TextWrapping = TextWrapping.Wrap,
                                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                                        FontFamily = new FontFamily("Consolas"),
                                        FontSize = 11
                                    };
                                    inspectorPreview.Content = textBlock;
                                }
                                else if (isImage)
                                {
                                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                                    bitmap.BeginInit();
                                    bitmap.UriSource = new Uri(tempFile);
                                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                                    bitmap.EndInit();
                                    var img = new Image { Source = bitmap, Margin = new Thickness(5) };
                                    inspectorPreview.Content = img;
                                }
                                else
                                {
                                    inspectorPreview.Content = new TextBlock { Text = "Preview not supported.", Foreground = Brushes.Gray, Margin = new Thickness(10) };
                                }
                            }
                            catch (Exception ex)
                            {
                                inspectorPreview.Content = new TextBlock { Text = "Preview error: " + ex.Message, Foreground = Brushes.Red, Margin = new Thickness(10) };
                            }
                            finally
                            {
                                // clean up preview temp file after a brief delay
                                ThreadPool.QueueUserWorkItem((o) => {
                                    try { Thread.Sleep(1000); File.Delete(tempFile); } catch {}
                                });
                            }
                        }
                        else
                        {
                            inspectorPreview.Content = new TextBlock { Text = "Could not retrieve preview data from device.", Foreground = Brushes.Gray, Margin = new Thickness(10) };
                        }
                    });
                });
            }
            else
            {
                // Local preview
                try
                {
                    if (isText)
                    {
                        string text = File.ReadAllText(selected.Path);
                        if (text.Length > 20000) text = text.Substring(0, 20000) + "\r\n... [TRUNCATED] ...";
                        inspectorPreview.Content = new TextBox
                        {
                            Text = text,
                            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#07080c")),
                            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e0e0e0")),
                            BorderThickness = new Thickness(0),
                            IsReadOnly = true,
                            TextWrapping = TextWrapping.Wrap,
                            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                            FontFamily = new FontFamily("Consolas"),
                            FontSize = 11
                        };
                    }
                    else if (isImage)
                    {
                        var bitmap = new System.Windows.Media.Imaging.BitmapImage(new Uri(selected.Path));
                        inspectorPreview.Content = new Image { Source = bitmap, Margin = new Thickness(5) };
                    }
                    else
                    {
                        inspectorPreview.Content = new TextBlock { Text = "Preview not supported.", Foreground = Brushes.Gray, Margin = new Thickness(10) };
                    }
                }
                catch (Exception ex)
                {
                    inspectorPreview.Content = new TextBlock { Text = "Preview error: " + ex.Message, Foreground = Brushes.Red, Margin = new Thickness(10) };
                }
            }
        }

        private void PathTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TextBox box = sender as TextBox;
                if (box == null || activeTab == null) return;
                
                string target = box.Text.Trim();
                // Map display shortcuts back to paths if appropriate
                if (target.StartsWith("USB of Babel:\\"))
                {
                    target = @"::{20D04FE0-3AEA-1069-A2D8-08002B30309D}\\\?\usb#vid_303a&pid_4020#c41e7ad4db1c#{6ac27878-a6fa-4155-ba85-f98f491d4f33}\" + target.Substring("USB of Babel:\\".Length);
                }
                
                if (target.StartsWith("Device:\\") && target.Contains("usb#"))
                {
                    // Preserve WPD format
                }
                else if (!target.StartsWith("::") && !Directory.Exists(target))
                {
                    MessageBox.Show("Directory not found.");
                    return;
                }

                activeTab.NavigateTo(target);
                UpdatePaneUI();
            }
        }

        private bool CopyWpdFileToLocal(string wpdPath, string localDestPath)
        {
            try
            {
                // Get the parent folder namespace
                int lastSlash = wpdPath.LastIndexOf('\\');
                if (lastSlash < 0) return false;
                string wpdFolder = wpdPath.Substring(0, lastSlash);
                string fileName = wpdPath.Substring(lastSlash + 1);

                dynamic folderObj = ResolveWpdFolder(wpdFolder);
                if (folderObj == null) return false;

                dynamic fileItemObj = null;
                foreach (dynamic item in folderObj.Items())
                {
                    string itemName = item.Name;
                    string itemPath = item.Path;
                    string lastPart = System.IO.Path.GetFileName(itemPath);
                    if (string.IsNullOrEmpty(lastPart))
                    {
                        int lastBS = itemPath.LastIndexOf('\\');
                        if (lastBS >= 0) lastPart = itemPath.Substring(lastBS + 1);
                        else lastPart = itemPath;
                    }

                    if (string.Equals(itemName, fileName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(itemPath, wpdPath, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(lastPart, fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        fileItemObj = item;
                        break;
                    }
                }

                if (fileItemObj == null) return false;

                // Ensure local folder exists
                string localFolder = System.IO.Path.GetDirectoryName(localDestPath);
                if (!Directory.Exists(localFolder)) Directory.CreateDirectory(localFolder);

                Type shellType = Type.GetTypeFromProgID("Shell.Application");
                object shellObj = System.Activator.CreateInstance(shellType);
                dynamic shell = shellObj;
                dynamic destFolderObj = shell.NameSpace(localFolder);
                if (destFolderObj == null) return false;

                // Perform copy
                destFolderObj.CopyHere(fileItemObj);

                // Wait for file arrival (Shell CopyHere is async)
                string expectedLocalPath = System.IO.Path.Combine(localFolder, fileItemObj.Name);
                int timeout = 100; // 5 seconds
                while (!File.Exists(expectedLocalPath) && timeout > 0)
                {
                    Thread.Sleep(50);
                    timeout--;
                }

                if (File.Exists(expectedLocalPath))
                {
                    if (expectedLocalPath != localDestPath)
                    {
                        if (File.Exists(localDestPath)) File.Delete(localDestPath);
                        File.Move(expectedLocalPath, localDestPath);
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("CopyWpdFileToLocal failed: " + ex.Message);
            }
            return false;
        }

        private bool CopyLocalToWpd(string localFilePath, string wpdFolderPath)
        {
            try
            {
                dynamic wpdFolderObj = ResolveWpdFolder(wpdFolderPath);
                if (wpdFolderObj == null) return false;

                wpdFolderObj.CopyHere(localFilePath);
                
                // Wait briefly for execution
                Thread.Sleep(500);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("CopyLocalToWpd failed: " + ex.Message);
            }
            return false;
        }

        private bool CopyWpdToWpd(string sourceWpdPath, string destWpdFolderPath)
        {
            // MTP-to-MTP direct copies are unsupported by Shell. Copy through local temp.
            try
            {
                string tempFile = Path.Combine(Path.GetTempPath(), Path.GetFileName(sourceWpdPath));
                if (CopyWpdFileToLocal(sourceWpdPath, tempFile))
                {
                    bool success = CopyLocalToWpd(tempFile, destWpdFolderPath);
                    File.Delete(tempFile);
                    return success;
                }
            }
            catch { }
            return false;
        }

        // File operations implementation
        private void TriggerCopy()
        {
            var activeListView = isLeftPaneActive ? leftFileListView : rightFileListView;
            var selected = activeListView.SelectedItem as FileItem;
            if (selected == null) return;

            clipboardSourcePath = selected.Path;
            clipboardIsCut = false;
            SetStatus(string.Format("Copied: {0} to clipboard.", selected.Name));
        }

        private void TriggerCut()
        {
            var activeListView = isLeftPaneActive ? leftFileListView : rightFileListView;
            var selected = activeListView.SelectedItem as FileItem;
            if (selected == null) return;

            clipboardSourcePath = selected.Path;
            clipboardIsCut = true;
            SetStatus(string.Format("Cut: {0} to clipboard.", selected.Name));
        }

        private void TriggerPaste()
        {
            if (string.IsNullOrEmpty(clipboardSourcePath) || activeTab == null) return;

            string destFolder = activeTab.CurrentPath;
            string fileName = Path.GetFileName(clipboardSourcePath);
            if (string.IsNullOrEmpty(fileName)) fileName = "WpdFile"; // fallback

            SetStatus("Performing paste operation...");
            ThreadPool.QueueUserWorkItem((state) => {
                bool success = false;
                
                bool srcIsWpd = clipboardSourcePath.StartsWith("::");
                bool destIsWpd = destFolder.StartsWith("::");

                if (!srcIsWpd && !destIsWpd)
                {
                    // Local to Local
                    string destPath = Path.Combine(destFolder, fileName);
                    try
                    {
                        if (clipboardIsCut)
                        {
                            if (File.Exists(clipboardSourcePath)) File.Move(clipboardSourcePath, destPath);
                            else if (Directory.Exists(clipboardSourcePath)) Directory.Move(clipboardSourcePath, destPath);
                        }
                        else
                        {
                            if (File.Exists(clipboardSourcePath)) File.Copy(clipboardSourcePath, destPath, true);
                            else if (Directory.Exists(clipboardSourcePath))
                            {
                                // Recursive directory copy helper
                                CopyDirectoryRecursive(clipboardSourcePath, destPath);
                            }
                        }
                        success = true;
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(new Action(() => MessageBox.Show("Paste error: " + ex.Message)));
                    }
                }
                else if (!srcIsWpd && destIsWpd)
                {
                    // Local to WPD
                    success = CopyLocalToWpd(clipboardSourcePath, destFolder);
                }
                else if (srcIsWpd && !destIsWpd)
                {
                    // WPD to Local
                    string destPath = Path.Combine(destFolder, fileName);
                    success = CopyWpdFileToLocal(clipboardSourcePath, destPath);
                }
                else if (srcIsWpd && destIsWpd)
                {
                    // WPD to WPD
                    success = CopyWpdToWpd(clipboardSourcePath, destFolder);
                }

                Dispatcher.Invoke(new Action(() => {
                    if (success)
                    {
                        SetStatus("Paste completed.");
                        UpdatePaneUI();
                        if (clipboardIsCut) clipboardSourcePath = null; // clear after cut paste
                    }
                    else
                    {
                        SetStatus("Paste failed.");
                    }
                }));
            });
        }

        private void CopyDirectoryRecursive(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
            }
            foreach (string sub in Directory.GetDirectories(sourceDir))
            {
                CopyDirectoryRecursive(sub, Path.Combine(destDir, Path.GetFileName(sub)));
            }
        }

        private void TriggerDelete()
        {
            var activeListView = isLeftPaneActive ? leftFileListView : rightFileListView;
            var selected = activeListView.SelectedItem as FileItem;
            if (selected == null) return;

            var result = MessageBox.Show(string.Format("Are you sure you want to delete {0}?", selected.Name), "Confirm Delete", MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes) return;

            if (selected.IsWpd)
            {
                // direct deletion on WPD via shell object
                try
                {
                    Type shellType = Type.GetTypeFromProgID("Shell.Application");
                    object shell = System.Activator.CreateInstance(shellType);
                    int lastSlash = selected.Path.LastIndexOf('\\');
                    if (lastSlash >= 0)
                    {
                        string wpdParent = selected.Path.Substring(0, lastSlash);
                        dynamic parentFolder = ((dynamic)shell).NameSpace(wpdParent);
                        if (parentFolder != null)
                        {
                            dynamic itemToDelete = null;
                            foreach (dynamic item in parentFolder.Items())
                            {
                                if (item.Name == selected.Name)
                                {
                                    itemToDelete = item;
                                    break;
                                }
                            }
                            if (itemToDelete != null)
                            {
                                // Invoke delete verb or show message
                                MessageBox.Show("Please delete items on this MTP device using Windows Explorer (direct shell deletion not allowed on this firmware partition).");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Delete failed on device: " + ex.Message);
                }
            }
            else
            {
                try
                {
                    if (selected.IsDirectory) Directory.Delete(selected.Path, true);
                    else File.Delete(selected.Path);
                    SetStatus("Deleted " + selected.Name);
                    UpdatePaneUI();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Delete error: " + ex.Message);
                }
            }
        }

        private void TriggerRename()
        {
            var activeListView = isLeftPaneActive ? leftFileListView : rightFileListView;
            var selected = activeListView.SelectedItem as FileItem;
            if (selected == null || selected.IsWpd) return; // WPD renames not supported natively

            // Simple dialog input
            string newName = Microsoft.VisualBasic.Interaction.InputBox("Enter new name:", "Rename Item", selected.Name);
            if (string.IsNullOrEmpty(newName) || newName == selected.Name) return;

            string parent = Path.GetDirectoryName(selected.Path);
            string newPath = Path.Combine(parent, newName);

            try
            {
                if (selected.IsDirectory) Directory.Move(selected.Path, newPath);
                else File.Move(selected.Path, newPath);
                SetStatus(string.Format("Renamed {0} to {1}", selected.Name, newName));
                UpdatePaneUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Rename error: " + ex.Message);
            }
        }

        // Global Hotkeys and Vim controls
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Check for command palette open state
            if (commandPaletteOverlay.Visibility == Visibility.Visible)
            {
                if (e.Key == Key.Escape)
                {
                    ToggleCommandPalette();
                    e.Handled = true;
                }
                return;
            }

            // Global commands
            if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.P)
            {
                ToggleCommandPalette();
                e.Handled = true;
                return;
            }

            // Pane toggling using Tab key
            if (e.Key == Key.Tab && !(Keyboard.FocusedElement is TextBox))
            {
                SetActivePane(!isLeftPaneActive);
                var activeListView = isLeftPaneActive ? leftFileListView : rightFileListView;
                activeListView.Focus();
                e.Handled = true;
                return;
            }

            // Vim controls (active when path box is not focused)
            if (!(Keyboard.FocusedElement is TextBox))
            {
                var activeListView = isLeftPaneActive ? leftFileListView : rightFileListView;
                if (e.Key == Key.J)
                {
                    // Move selection down
                    if (activeListView.Items.Count > 0)
                    {
                        int index = activeListView.SelectedIndex;
                        if (index < activeListView.Items.Count - 1)
                        {
                            activeListView.SelectedIndex = index + 1;
                            activeListView.ScrollIntoView(activeListView.SelectedItem);
                        }
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.K)
                {
                    // Move selection up
                    if (activeListView.Items.Count > 0)
                    {
                        int index = activeListView.SelectedIndex;
                        if (index > 0)
                        {
                            activeListView.SelectedIndex = index - 1;
                            activeListView.ScrollIntoView(activeListView.SelectedItem);
                        }
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.H)
                {
                    // Go back
                    GoBack(isLeftPaneActive);
                    e.Handled = true;
                }
                else if (e.Key == Key.L)
                {
                    // Open folder / activate file
                    FileListView_DoubleClick(activeListView);
                    e.Handled = true;
                }
                else if (e.Key == Key.U)
                {
                    // Go up directory level
                    GoUp(isLeftPaneActive);
                    e.Handled = true;
                }
                
                // Copy/Paste shortcuts
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    if (e.Key == Key.C)
                    {
                        TriggerCopy();
                        e.Handled = true;
                    }
                    else if (e.Key == Key.X)
                    {
                        TriggerCut();
                        e.Handled = true;
                    }
                    else if (e.Key == Key.V)
                    {
                        TriggerPaste();
                        e.Handled = true;
                    }
                    else if (e.Key == Key.T)
                    {
                        AddTab(isLeftPaneActive);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.W)
                    {
                        CloseTab(isLeftPaneActive, activeTabIndex);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.S)
                    {
                        // Toggle dual pane split view
                        ToggleSplitPane();
                        e.Handled = true;
                    }
                }
                
                if (e.Key == Key.Delete)
                {
                    TriggerDelete();
                    e.Handled = true;
                }
                else if (e.Key == Key.F2)
                {
                    TriggerRename();
                    e.Handled = true;
                }
            }
        }

        private void ToggleSplitPane()
        {
            isSplit = !isSplit;
            if (isSplit)
            {
                rightPaneGrid.Visibility = Visibility.Visible;
                paneSplitter.Visibility = Visibility.Visible;
                // split columns evenly
                var contentGrid = leftPaneBorder.Parent as Grid;
                if (contentGrid != null)
                {
                    contentGrid.ColumnDefinitions[1].Width = new GridLength(3); // Splitter visible
                    contentGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star); // Right Pane visible
                }
                SetStatus("Dual Pane split layout activated.");
            }
            else
            {
                rightPaneGrid.Visibility = Visibility.Collapsed;
                paneSplitter.Visibility = Visibility.Collapsed;
                // collapse right column
                var contentGrid = leftPaneBorder.Parent as Grid;
                if (contentGrid != null)
                {
                    contentGrid.ColumnDefinitions[1].Width = new GridLength(0); // Splitter collapsed
                    contentGrid.ColumnDefinitions[2].Width = new GridLength(0); // Right Pane collapsed
                }
                SetActivePane(true); // force left pane active
                SetStatus("Single Pane layout activated.");
            }
        }

        // Command Palette Logic
        private struct PaletteCommand
        {
            public string Name { get; set; }
            public Action Action { get; set; }
            public override string ToString() { return Name; }
        }

        private List<PaletteCommand> paletteCommands = new List<PaletteCommand>();

        private void SetupPaletteCommands()
        {
            paletteCommands.Clear();
            paletteCommands.Add(new PaletteCommand { Name = "Dual Pane: Toggle Split View (Ctrl S)", Action = ToggleSplitPane });
            paletteCommands.Add(new PaletteCommand { Name = "Tabs: Open New Tab (Ctrl+T)", Action = () => AddTab(isLeftPaneActive) });
            paletteCommands.Add(new PaletteCommand { Name = "Tabs: Close Active Tab (Ctrl+W)", Action = () => CloseTab(isLeftPaneActive, activeTabIndex) });
            paletteCommands.Add(new PaletteCommand { Name = "File: Copy Selection (Ctrl+C)", Action = TriggerCopy });
            paletteCommands.Add(new PaletteCommand { Name = "File: Cut Selection (Ctrl+X)", Action = TriggerCut });
            paletteCommands.Add(new PaletteCommand { Name = "File: Paste Clipboard (Ctrl+V)", Action = TriggerPaste });
            paletteCommands.Add(new PaletteCommand { Name = "File: Rename Selected Item (F2)", Action = TriggerRename });
            paletteCommands.Add(new PaletteCommand { Name = "File: Delete Selected Item (Delete)", Action = TriggerDelete });
            paletteCommands.Add(new PaletteCommand { Name = "Navigation: Up One Directory Level (Backspace / U)", Action = () => GoUp(isLeftPaneActive) });
            paletteCommands.Add(new PaletteCommand { Name = "Navigation: Refresh Directory Contents (F5)", Action = UpdatePaneUI });
            paletteCommands.Add(new PaletteCommand { Name = "Devices: Refresh Hardware / WPD List", Action = () => { RefreshSidebar(); SetStatus("Hardware device list updated."); } });
            paletteCommands.Add(new PaletteCommand { Name = "System: Toggle Hidden Files Visibility", Action = () => { ShowHiddenFiles = !ShowHiddenFiles; UpdatePaneUI(); SetStatus("Hidden files toggle: " + ShowHiddenFiles); } });
            paletteCommands.Add(new PaletteCommand { Name = "Help: Show Keybindings / Shortcuts", Action = ShowHelpDialog });
        }

        private void ToggleCommandPalette()
        {
            if (commandPaletteOverlay.Visibility == Visibility.Visible)
            {
                commandPaletteOverlay.Visibility = Visibility.Collapsed;
                var activeListView = isLeftPaneActive ? leftFileListView : rightFileListView;
                activeListView.Focus();
            }
            else
            {
                SetupPaletteCommands();
                commandPaletteSearch.Text = "";
                commandPaletteOverlay.Visibility = Visibility.Visible;
                FilterPaletteCommands();
                commandPaletteSearch.Focus();
            }
        }

        private void FilterPaletteCommands()
        {
            string filter = commandPaletteSearch.Text.ToLower().Trim();
            var filtered = paletteCommands.Where(cmd => cmd.Name.ToLower().Contains(filter)).ToList();
            commandPaletteList.ItemsSource = filtered;
            if (commandPaletteList.Items.Count > 0)
            {
                commandPaletteList.SelectedIndex = 0;
            }
        }

        private void ExecutePaletteCommand()
        {
            var selected = commandPaletteList.SelectedItem as PaletteCommand?;
            if (selected != null)
            {
                ToggleCommandPalette();
                selected.Value.Action.Invoke();
            }
        }

        private void ShowHelpDialog()
        {
            string helpText = @"SAGITTARIUS Explorer Keybindings:
* J / K : Move cursor down / up (Vim style)
* H / L : Go back in history / Open folder (Vim style)
* U : Go up one directory level
* Tab : Toggle focus between active panes
* Ctrl S : Toggle split view (Single or Dual Pane)
* Ctrl T : Open new tab
* Ctrl W : Close active tab
* Ctrl C or Ctrl X : Copy or Cut selected item
* Ctrl V : Paste item
* F2 : Rename selected item
* Delete : Delete selected item
* Ctrl Shift P : Open Command Palette";
            MessageBox.Show(helpText, "Sagittarius Explorer Help", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // P/Invoke for File Properties Dialog
        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private struct SHELLEXECUTEINFO
        {
            public int cbSize;
            public uint fMask;
            public IntPtr hwnd;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPTStr)]
            public string lpVerb;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPTStr)]
            public string lpFile;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPTStr)]
            public string lpParameters;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPTStr)]
            public string lpDirectory;
            public int nShow;
            public IntPtr hInstApp;
            public IntPtr lpIDList;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPTStr)]
            public string lpClass;
            public IntPtr hKeyClass;
            public uint dwHotKey;
            public IntPtr hIcon;
            public IntPtr hProcess;
        }

        private const uint SEE_MASK_INVOKEIDLIST = 0x0000000c;

        private static void ShowFileProperties(string filePath)
        {
            try
            {
                SHELLEXECUTEINFO sei = new SHELLEXECUTEINFO();
                sei.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(sei);
                sei.lpVerb = "properties";
                sei.lpFile = filePath;
                sei.nShow = 5; // SW_SHOW
                sei.fMask = SEE_MASK_INVOKEIDLIST;
                ShellExecuteEx(ref sei);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not show properties: " + ex.Message);
            }
        }

        // Custom Styled Context Menu mimicking File Pilot
        private void ShowCustomContextMenu(ListView listView)
        {
            var menu = new ContextMenu();

            // 1. Search Box Grid
            var searchGrid = new Grid();
            searchGrid.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#07080c"));
            searchGrid.Margin = new Thickness(0, 0, 0, 4);
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            searchGrid.Height = 30;

            var searchIcon = new TextBlock { Text = "", Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#606e88")), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, FontSize = 11 };
            
            var searchBox = new TextBox
            {
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#606e88")),
                Text = "Select a system command...",
                BorderThickness = new Thickness(0),
                VerticalContentAlignment = VerticalAlignment.Center,
                FontSize = 11.5,
                Padding = new Thickness(0)
            };

            var closeBtn = new Button
            {
                Content = "x",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#606e88")),
                BorderThickness = new Thickness(0),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            closeBtn.Click += (s, e) => menu.IsOpen = false;

            // Placeholder handling
            searchBox.GotFocus += (s, e) => {
                if (searchBox.Text == "Select a system command...")
                {
                    searchBox.Text = "";
                    searchBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00f0ff"));
                }
            };
            searchBox.LostFocus += (s, e) => {
                if (string.IsNullOrEmpty(searchBox.Text))
                {
                    searchBox.Text = "Select a system command...";
                    searchBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#606e88"));
                }
            };

            searchGrid.Children.Add(searchIcon);
            Grid.SetColumn(searchIcon, 0);
            searchGrid.Children.Add(searchBox);
            Grid.SetColumn(searchBox, 1);
            searchGrid.Children.Add(closeBtn);
            Grid.SetColumn(closeBtn, 2);

            menu.Items.Add(searchGrid);

            // Separator helper
            Action addSep = () => menu.Items.Add(new Separator { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1a2233")), Margin = new Thickness(0, 2, 0, 2) });

            // MenuItem helper
            Func<string, string, Action, MenuItem> createItem = (header, gesture, action) =>
            {
                var item = new MenuItem();
                item.Header = header;
                item.InputGestureText = gesture;
                item.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ff5500"));
                item.Background = Brushes.Transparent;
                item.Padding = new Thickness(8, 4, 8, 4);
                item.FontSize = 11.5;
                if (action != null)
                {
                    item.Click += (s, e) => action();
                }
                return item;
            };

            // Search filtering
            searchBox.TextChanged += (s, e) =>
            {
                string filter = searchBox.Text.ToLower().Trim();
                if (filter == "select a system command...") filter = "";
                foreach (var item in menu.Items)
                {
                    var menuItem = item as MenuItem;
                    if (menuItem != null)
                    {
                        var headerStr = menuItem.Header as string;
                        if (headerStr != null)
                        {
                            bool matches = string.IsNullOrEmpty(filter) || headerStr.ToLower().Contains(filter);
                            menuItem.Visibility = matches ? Visibility.Visible : Visibility.Collapsed;
                        }
                    }
                    else
                    {
                        var sep = item as Separator;
                        if (sep != null)
                        {
                            sep.Visibility = string.IsNullOrEmpty(filter) ? Visibility.Visible : Visibility.Collapsed;
                        }
                    }
                }
            };

            var selectedFile = listView.SelectedItem as FileItem;
            string selectedPath = selectedFile != null ? selectedFile.Path : null;

            menu.Items.Add(createItem("Open", "", () => { if (selectedFile != null) FileListView_DoubleClick(listView); }));
            menu.Items.Add(createItem("Delete", "Delete", () => { TriggerDelete(); }));
            menu.Items.Add(createItem("Rename", "Rename F2", () => { TriggerRename(); }));
            menu.Items.Add(createItem("Properties", "", () => { if (!string.IsNullOrEmpty(selectedPath)) ShowFileProperties(selectedPath); }));
            
            addSep();

            menu.Items.Add(createItem("Open in Terminal", "", () => {
                string dir = (selectedFile != null && selectedFile.IsDirectory) ? selectedPath : GetActiveTabDirectory();
                if (Directory.Exists(dir)) Process.Start("powershell.exe", string.Format("-NoExit -Command \"cd '{0}'\"", dir));
            }));
            
            menu.Items.Add(createItem("Open with Code", "", () => {
                if (!string.IsNullOrEmpty(selectedPath)) Process.Start("code", string.Format("\"{0}\"", selectedPath));
            }));

            menu.Items.Add(createItem("Open Git GUI here", "", () => {
                string dir = GetActiveTabDirectory();
                if (Directory.Exists(dir)) Process.Start("git-gui.exe", string.Format("\"{0}\"", dir));
            }));

            menu.Items.Add(createItem("Open Git Bash here", "", () => {
                string dir = GetActiveTabDirectory();
                if (Directory.Exists(dir)) Process.Start("git-bash.exe", string.Format("--cd=\"{0}\"", dir));
            }));

            menu.Items.Add(createItem("Open in File Pilot", "", () => {
                if (!string.IsNullOrEmpty(selectedPath)) Process.Start("explorer.exe", string.Format("/select,\"{0}\"", selectedPath));
                else Process.Start("explorer.exe", string.Format("\"{0}\"", GetActiveTabDirectory()));
            }));

            menu.Items.Add(createItem("Upload with ShareX", "", () => {
                if (!string.IsNullOrEmpty(selectedPath)) Process.Start("ShareX.exe", string.Format("\"{0}\"", selectedPath));
            }));

            var zipMenu = new MenuItem { Header = "7Zip", Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ff5500")), FontSize = 11.5 };
            zipMenu.Items.Add(createItem("Extract Here", "", () => { }));
            zipMenu.Items.Add(createItem("Add to Archive...", "", () => { }));
            menu.Items.Add(zipMenu);

            menu.Items.Add(createItem("Bulk Rename Here", "", () => { }));
            menu.Items.Add(createItem("Bulk Rename Selected Items", "", () => { }));
            menu.Items.Add(createItem("Scan with Microsoft Defender...", "", () => {
                if (!string.IsNullOrEmpty(selectedPath)) Process.Start("MpCmdRun.exe", string.Format("-Scan -ScanType 3 -File \"{0}\"", selectedPath));
            }));
            menu.Items.Add(createItem("What's locking this folder?", "", () => { }));

            addSep();

            var accessMenu = new MenuItem { Header = "Give access to", Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ff5500")), FontSize = 11.5 };
            accessMenu.Items.Add(createItem("Remove access", "", null));
            menu.Items.Add(accessMenu);

            menu.Items.Add(createItem("Restore previous versions", "", null));

            var libraryMenu = new MenuItem { Header = "Include in library", Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ff5500")), FontSize = 11.5 };
            libraryMenu.Items.Add(createItem("Documents", "", null));
            libraryMenu.Items.Add(createItem("Music", "", null));
            menu.Items.Add(libraryMenu);

            var pinItem = createItem("Pin to Sidebar", "", () => {
                if (selectedFile != null && selectedFile.IsDirectory)
                {
                    PinFolderToSidebar(selectedPath);
                }
            });
            if (selectedFile == null || !selectedFile.IsDirectory)
            {
                pinItem.IsEnabled = false;
            }
            menu.Items.Add(pinItem);
            menu.Items.Add(createItem("Copy as path", "", () => {
                if (!string.IsNullOrEmpty(selectedPath)) Clipboard.SetText(selectedPath);
            }));

            menu.Items.Add(createItem("Unlock with File Locksmith", "", null));
            menu.Items.Add(createItem("Rename with PowerRename", "", null));

            addSep();

            var sendMenu = new MenuItem { Header = "Send to", Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ff5500")), FontSize = 11.5 };
            sendMenu.Items.Add(createItem("Desktop (create shortcut)", "", null));
            menu.Items.Add(sendMenu);

            addSep();

            menu.Items.Add(createItem("Cut", "Ctrl+X", () => { TriggerCut(); }));
            menu.Items.Add(createItem("Copy", "Ctrl+C  Ctrl+Insert  Y", () => { TriggerCopy(); }));
            menu.Items.Add(createItem("Create shortcut", "", () => { }));

            var menuStyle = new Style(typeof(ContextMenu));
            menuStyle.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0f131c"))));
            menuStyle.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1a2233"))));
            menuStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            menu.Style = menuStyle;

            menu.IsOpen = true;
            menu.Opened += (s, ev) => searchBox.Focus();
        }

        private string GetActiveTabDirectory()
        {
            var tab = isLeftPaneActive ? (leftTabs.Count > 0 ? leftTabs[leftActiveTabIndex] : null) : (rightTabs.Count > 0 ? rightTabs[rightActiveTabIndex] : null);
            return tab != null ? tab.CurrentPath : @"C:\";
        }

        // Drag-and-Drop Handlers
        private Point dragStartPoint;

        private void FileListView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            dragStartPoint = e.GetPosition(null);
        }

        private void FileListView_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point mousePos = e.GetPosition(null);
                Vector diff = dragStartPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    var listView = sender as ListView;
                    if (listView == null) return;
                    
                    var selectedItems = listView.SelectedItems;
                    if (selectedItems.Count == 0) return;

                    var paths = new System.Collections.Specialized.StringCollection();
                    foreach (FileItem item in selectedItems)
                    {
                        if (!item.IsWpd && (File.Exists(item.Path) || Directory.Exists(item.Path)))
                        {
                            paths.Add(item.Path);
                        }
                    }

                    if (paths.Count > 0)
                    {
                        var dataObject = new DataObject();
                        dataObject.SetFileDropList(paths);

                        // Start visual
                        var firstItem = selectedItems[0] as FileItem;
                        string firstIcon = firstItem != null ? firstItem.Icon : "File";
                        string dragName = selectedItems.Count > 1 
                            ? string.Format("{0} items", selectedItems.Count) 
                            : (firstItem != null ? firstItem.Name : "File");

                        StartDragVisual(firstIcon, dragName);

                        GiveFeedbackEventHandler feedbackHandler = (s, ev) =>
                        {
                            UpdateDragVisualPosition();
                        };

                        listView.GiveFeedback += feedbackHandler;
                        try
                        {
                            DragDrop.DoDragDrop(listView, dataObject, DragDropEffects.Copy | DragDropEffects.Move);
                        }
                        finally
                        {
                            listView.GiveFeedback -= feedbackHandler;
                            StopDragVisual();
                        }
                    }
                }
            }
        }

        private void FileListView_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void FileListView_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                var listView = sender as ListView;
                bool isLeft = (listView == leftFileListView);
                var tab = isLeft ? (leftTabs.Count > 0 ? leftTabs[leftActiveTabIndex] : null) : (rightTabs.Count > 0 ? rightTabs[rightActiveTabIndex] : null);
                if (tab == null) return;

                string destDir = tab.CurrentPath;
                if (!Directory.Exists(destDir)) return;

                try
                {
                    foreach (var file in files)
                    {
                        string destPath = Path.Combine(destDir, Path.GetFileName(file));
                        if (File.Exists(file))
                        {
                            File.Copy(file, destPath, true);
                        }
                        else if (Directory.Exists(file))
                        {
                            CopyDirectory(file, destPath);
                        }
                    }
                    UpdatePaneUI();
                    SetStatus("Dropped files successfully copied.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error copying dropped items: " + ex.Message);
                }
            }
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
            }
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                CopyDirectory(subDir, Path.Combine(destDir, Path.GetFileName(subDir)));
            }
        }

        // Pinned/Customizable Folders persistence and manipulation
        private void CreateDefaultPinnedFolders()
        {
            try
            {
                var defaults = new List<string>
                {
                    @"C:\Users\jluka\Desktop\|Desktop|Desktop",
                    @"C:\Users\jluka\Downloads\|Downloads|Downloads",
                    @"D:\jluka\Downloads\|Downloads|Downloads",
                    @"C:\$Recycle.Bin\|$Recycle.Bin|RecycleBin",
                    @"C:\Users\jluka\Documents\|Documents|Documents",
                    @"D:\jluka\Documents\|Documents|Documents",
                    @"C:\Users\jluka\Pictures\|Pictures|Pictures",
                    @"C:\Users\jluka\Pictures\Screenshots\|Screenshots|Screenshots",
                    @"C:\Users\jluka\Documents\Antigravity\|Antigravity|Antigravity",
                    @"C:\Users\jluka\Documents\Torn Scripts\|Torn Scripts|TornScripts"
                };
                File.WriteAllLines(pinnedFilePath, defaults);
            }
            catch { }
        }

        private void LoadPinnedFolders()
        {
            pinnedFolders.Clear();
            try
            {
                if (!File.Exists(pinnedFilePath))
                {
                    CreateDefaultPinnedFolders();
                }

                if (File.Exists(pinnedFilePath))
                {
                    foreach (var line in File.ReadAllLines(pinnedFilePath))
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed)) continue;

                        var parts = trimmed.Split('|');
                        if (parts.Length >= 1)
                        {
                            string path = parts[0];
                            string name = parts.Length >= 2 ? parts[1] : Path.GetFileName(path);
                            if (string.IsNullOrEmpty(name)) name = path;
                            string icon = parts.Length >= 3 ? parts[2] : "Folder";

                            if (Directory.Exists(path) || path.StartsWith("::") || path.Length <= 3)
                            {
                                pinnedFolders.Add(new PinnedFolder { Path = path, Name = name, Icon = icon });
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private void SavePinnedFolders()
        {
            try
            {
                var lines = new List<string>();
                foreach (var pf in pinnedFolders)
                {
                    lines.Add(string.Format("{0}|{1}|{2}", pf.Path, pf.Name, pf.Icon));
                }
                File.WriteAllLines(pinnedFilePath, lines);
            }
            catch { }
        }

        private int FindPinnedFolderIndex(string path)
        {
            if (string.IsNullOrEmpty(path)) return -1;
            for (int i = 0; i < pinnedFolders.Count; i++)
            {
                if (pinnedFolders[i].Path.Equals(path, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        private void PinFolderToSidebar(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (FindPinnedFolderIndex(path) < 0)
            {
                string name = Path.GetFileName(path);
                if (string.IsNullOrEmpty(name)) name = path;
                if (path.EndsWith(":\\")) name = path;

                pinnedFolders.Add(new PinnedFolder { Path = path, Name = name, Icon = "Folder" });
                SavePinnedFolders();
                RefreshSidebar();
                SetStatus("Folder pinned to sidebar.");
            }
        }

        private void UnpinFolderFromSidebar(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            int idx = FindPinnedFolderIndex(path);
            if (idx >= 0)
            {
                pinnedFolders.RemoveAt(idx);
                SavePinnedFolders();
                RefreshSidebar();
                SetStatus("Folder removed from sidebar.");
            }
        }

        private void MovePinnedFolderUp(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            int idx = FindPinnedFolderIndex(path);
            if (idx > 0)
            {
                var temp = pinnedFolders[idx];
                pinnedFolders.RemoveAt(idx);
                pinnedFolders.Insert(idx - 1, temp);
                SavePinnedFolders();
                RefreshSidebar();
                SetStatus("Sidebar item moved up.");
            }
        }

        private void MovePinnedFolderDown(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            int idx = FindPinnedFolderIndex(path);
            if (idx >= 0 && idx < pinnedFolders.Count - 1)
            {
                var temp = pinnedFolders[idx];
                pinnedFolders.RemoveAt(idx);
                pinnedFolders.Insert(idx + 1, temp);
                SavePinnedFolders();
                RefreshSidebar();
                SetStatus("Sidebar item moved down.");
            }
        }

        // Sidebar List context menu and drag-drop reordering
        private void SidebarList_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null) return;

            DependencyObject dep = (DependencyObject)e.OriginalSource;
            while (dep != null && dep != listBox)
            {
                if (dep is ListBoxItem)
                {
                    var listBoxItem = (ListBoxItem)dep;
                    var selected = listBoxItem.Content as SidebarItem;
                    if (selected != null && selected.IsPinned)
                    {
                        listBox.SelectedItem = selected;

                        var menu = new ContextMenu();
                        Func<string, Action, MenuItem> createItem = (header, action) =>
                        {
                            var item = new MenuItem();
                            item.Header = header;
                            item.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ff5500"));
                            item.Background = Brushes.Transparent;
                            item.Padding = new Thickness(8, 4, 8, 4);
                            item.FontSize = 11.5;
                            if (action != null)
                            {
                                item.Click += (s, ev) => action();
                            }
                            return item;
                        };

                        int idx = FindPinnedFolderIndex(selected.Path);
                        bool isTop = (idx == 0);
                        bool isBottom = (idx == pinnedFolders.Count - 1);

                        menu.Items.Add(createItem("Unpin", () => UnpinFolderFromSidebar(selected.Path)));
                        if (!isTop)
                        {
                            menu.Items.Add(createItem("Move Up", () => MovePinnedFolderUp(selected.Path)));
                        }
                        if (!isBottom)
                        {
                            menu.Items.Add(createItem("Move Down", () => MovePinnedFolderDown(selected.Path)));
                        }

                        var menuStyle = new Style(typeof(ContextMenu));
                        menuStyle.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0f131c"))));
                        menuStyle.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1a2233"))));
                        menuStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
                        menu.Style = menuStyle;

                        menu.IsOpen = true;
                    }
                    break;
                }
                dep = VisualTreeHelper.GetParent(dep);
            }
        }

        private Point sidebarDragStartPoint;
        private SidebarItem draggedSidebarItem = null;

        private void SidebarList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            sidebarDragStartPoint = e.GetPosition(null);
            draggedSidebarItem = null;

            var listBox = sender as ListBox;
            if (listBox == null) return;

            DependencyObject dep = (DependencyObject)e.OriginalSource;
            while (dep != null && dep != listBox)
            {
                if (dep is ListBoxItem)
                {
                    var item = ((ListBoxItem)dep).Content as SidebarItem;
                    if (item != null && item.IsPinned)
                    {
                        draggedSidebarItem = item;
                    }
                    break;
                }
                dep = VisualTreeHelper.GetParent(dep);
            }
        }

        private void SidebarList_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && draggedSidebarItem != null)
            {
                Point mousePos = e.GetPosition(null);
                Vector diff = sidebarDragStartPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    var listBox = sender as ListBox;
                    if (listBox == null) return;

                    StartDragVisual(draggedSidebarItem.Icon, draggedSidebarItem.Name);

                    GiveFeedbackEventHandler feedbackHandler = (s, ev) =>
                    {
                        UpdateDragVisualPosition();
                    };

                    listBox.GiveFeedback += feedbackHandler;
                    try
                    {
                        DragDrop.DoDragDrop(listBox, draggedSidebarItem, DragDropEffects.Move);
                    }
                    finally
                    {
                        listBox.GiveFeedback -= feedbackHandler;
                        StopDragVisual();
                        draggedSidebarItem = null;
                    }
                }
            }
        }

        private void SidebarList_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(SidebarItem)))
            {
                var item = e.Data.GetData(typeof(SidebarItem)) as SidebarItem;
                if (item != null && item.IsPinned)
                {
                    e.Effects = DragDropEffects.Move;
                    e.Handled = true;
                    return;
                }
            }
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var listBox = sender as ListBox;
                if (listBox != null)
                {
                    DependencyObject dep = (DependencyObject)e.OriginalSource;
                    while (dep != null && dep != listBox)
                    {
                        if (dep is ListBoxItem)
                        {
                            var targetItem = ((ListBoxItem)dep).Content as SidebarItem;
                            if (targetItem != null && !targetItem.IsHeader && !string.IsNullOrEmpty(targetItem.Path) && Directory.Exists(targetItem.Path))
                            {
                                e.Effects = DragDropEffects.Copy;
                                e.Handled = true;
                                return;
                            }
                            break;
                        }
                        dep = VisualTreeHelper.GetParent(dep);
                    }
                }
            }
            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void SidebarList_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(SidebarItem)))
            {
                var droppedItem = e.Data.GetData(typeof(SidebarItem)) as SidebarItem;
                if (droppedItem != null && droppedItem.IsPinned)
                {
                    var listBox = sender as ListBox;
                    if (listBox == null) return;

                    DependencyObject dep = (DependencyObject)e.OriginalSource;
                    while (dep != null && dep != listBox)
                    {
                        if (dep is ListBoxItem)
                        {
                            var targetItem = ((ListBoxItem)dep).Content as SidebarItem;
                            if (targetItem != null && targetItem.IsPinned && targetItem != droppedItem)
                            {
                                int oldIdx = FindPinnedFolderIndex(droppedItem.Path);
                                int newIdx = FindPinnedFolderIndex(targetItem.Path);

                                if (oldIdx >= 0 && newIdx >= 0)
                                {
                                    var temp = pinnedFolders[oldIdx];
                                    pinnedFolders.RemoveAt(oldIdx);
                                    pinnedFolders.Insert(newIdx, temp);
                                    SavePinnedFolders();
                                    RefreshSidebar();
                                    SetStatus("Sidebar items reordered.");
                                }
                            }
                            break;
                        }
                        dep = VisualTreeHelper.GetParent(dep);
                    }
                }
            }
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                var listBox = sender as ListBox;
                if (listBox == null) return;

                DependencyObject dep = (DependencyObject)e.OriginalSource;
                while (dep != null && dep != listBox)
                {
                    if (dep is ListBoxItem)
                    {
                        var targetItem = ((ListBoxItem)dep).Content as SidebarItem;
                        if (targetItem != null && !targetItem.IsHeader && !string.IsNullOrEmpty(targetItem.Path) && Directory.Exists(targetItem.Path))
                        {
                            try
                            {
                                foreach (var file in files)
                                {
                                    string destPath = Path.Combine(targetItem.Path, Path.GetFileName(file));
                                    if (File.Exists(file))
                                    {
                                        File.Copy(file, destPath, true);
                                    }
                                    else if (Directory.Exists(file))
                                    {
                                        CopyDirectory(file, destPath);
                                    }
                                }
                                UpdatePaneUI();
                                SetStatus("Files successfully copied to folder.");
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show("Error copying files to folder: " + ex.Message);
                            }
                        }
                        break;
                    }
                    dep = VisualTreeHelper.GetParent(dep);
                }
            }
        }

        // Custom Styled Translucent Drag Visual overlay
        private Window dragVisualWindow = null;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out Win32Point lpPoint);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct Win32Point
        {
            public int X;
            public int Y;
        }

        private void StartDragVisual(string icon, string name)
        {
            try
            {
                if (dragVisualWindow != null)
                {
                    dragVisualWindow.Close();
                }

                dragVisualWindow = new Window
                {
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Background = Brushes.Transparent,
                    ShowInTaskbar = false,
                    Topmost = true,
                    IsHitTestVisible = false,
                    SizeToContent = SizeToContent.WidthAndHeight
                };

                var border = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e60f131c")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ff007f")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(8, 4, 8, 4)
                };

                var stack = new StackPanel { Orientation = Orientation.Horizontal };
                
                var iconText = new TextBlock
                {
                    Text = icon,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ff007f")),
                    Margin = new Thickness(0, 0, 6, 0),
                    FontSize = 12
                };

                var nameText = new TextBlock
                {
                    Text = name,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00f0ff")),
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold
                };

                stack.Children.Add(iconText);
                stack.Children.Add(nameText);
                border.Child = stack;
                dragVisualWindow.Content = border;

                UpdateDragVisualPosition();
                dragVisualWindow.Show();
            }
            catch { }
        }

        private void UpdateDragVisualPosition()
        {
            if (dragVisualWindow == null) return;
            try
            {
                Win32Point mousePos;
                if (GetCursorPos(out mousePos))
                {
                    double x = mousePos.X;
                    double y = mousePos.Y;
                    
                    PresentationSource source = PresentationSource.FromVisual(this);
                    if (source != null && source.CompositionTarget != null)
                    {
                        var matrix = source.CompositionTarget.TransformToDevice;
                        x = x / matrix.M11;
                        y = y / matrix.M22;
                    }
                    
                    dragVisualWindow.Left = x + 15;
                    dragVisualWindow.Top = y + 15;
                }
            }
            catch { }
        }

        private void StopDragVisual()
        {
            if (dragVisualWindow != null)
            {
                try
                {
                    dragVisualWindow.Close();
                }
                catch { }
                dragVisualWindow = null;
            }
        }

    }
}
