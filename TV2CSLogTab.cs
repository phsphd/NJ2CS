using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Timers;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net.WebSockets;
using System.Threading;
using System.Reflection;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Xml.Serialization;
using System.Xml.Linq; // For XDocument and XElement
using System.Text.RegularExpressions;
using NinjaTrader.Data;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;
using System.Windows.Threading;
using SchwabApiCS;
using WinForms = System.Windows.Forms;


namespace NinjaTrader.NinjaScript.AddOns
{ 

	public class TV2CSLogTab : NTTabPage
	{
	    private TextBox logTextBox;
	    private Account account;
	
	    public TV2CSLogTab()
	    {
	        logTextBox = new TextBox
	        {
	            IsReadOnly = true,
	            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
	            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
	            TextWrapping = TextWrapping.Wrap
	        };
	
	        var scrollViewer = new ScrollViewer
	        {
	            Content = logTextBox
	        };
	
	        Content = scrollViewer;
	
	        lock (Account.All)
	            account = Account.All.FirstOrDefault(a => a.Name == "Sim101");
	
	        if (account != null)
	            account.PositionUpdate += OnPositionUpdate;
	    }
	
	    private void OnPositionUpdate(object sender, PositionEventArgs e)
	    {
	        NinjaTrader.Code.Output.Process(
	            $"Instrument: {e.Position.Instrument.FullName} MarketPosition: {e.MarketPosition} " +
	            $"AveragePrice: {e.AveragePrice} Quantity: {e.Quantity}",
	            PrintTo.OutputTab1
	        );
	    }
		public void AddLogMessage(string message)
		{
		    if (logTextBox == null)
		        return;
		
		    try
		    {
		        if (logTextBox.Dispatcher.CheckAccess())
		        {
		            logTextBox.AppendText($"{DateTime.Now}: {message}\n");
		            logTextBox.ScrollToEnd();
		        }
		        else
		        {
		            logTextBox.Dispatcher.Invoke(() =>
		            {
		                try
		                {
		                    logTextBox.AppendText($"{DateTime.Now}: {message}\n");
		                    logTextBox.ScrollToEnd();
		                }
		                catch (Exception ex)
		                {
							NinjaTrader.Code.Output.Process($"{ex.Message}", PrintTo.OutputTab1);
		                }
		            });
		        }
		    }
		    catch (Exception ex)
		    {
				NinjaTrader.Code.Output.Process($"{ex.Message}", PrintTo.OutputTab1);
		    }
		}	
	    protected override string GetHeaderPart(string name)
	    {
	        return "Logs"; // Provide the custom header text for this tab
	    }
	    protected override void Save(XElement element)
	    {
	        // Implement save logic if necessary
	    }
	    protected override void Restore(XElement element)
	    {
	        // Implement restore logic if necessary
	    }
	    public override void Cleanup()
	    {
	        // Cleanup logic if required
	    }
	}
}