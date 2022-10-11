using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Auidio
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public Model.AudioRec recorder { get; set; }
        public ObservableCollection<String> Samples { get; set; }
   
        public MainWindow()
        {
            Samples = new ObservableCollection<string>();
            recorder = new Model.AudioRec();
            PlotControl = new ScottPlot.WpfPlot();
            recorder.AttachControlToModel(this);
            InitializeComponent();
            this.DataContext = this;
            recorder.StartRecording();
            //PlotControl.Plot.AddSignal(new double[] { 1,2,4,8,16,32,64}, 7);
            //PlotControl.Refresh();
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "(*.wav)|*.wav";
            var res = dlg.ShowDialog();
            recorder.AddFile(dlg.FileName, FileMenu.SelectedIndex);
            Samples.Add(dlg.FileName);
        }

        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {

        }
    }
}
