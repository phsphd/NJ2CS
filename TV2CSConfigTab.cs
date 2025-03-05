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
    public class TV2CSConfigTab : NTTabPage
	{
	    private StackPanel stackPanel;
	    private NJ2CS baseInstance;  // Reference to the base class
		public SchwabApi schwabApi ;
        private SchwabTokens schwabTokens;
        public Dictionary<string, string> csAccounts = new();	
	    public TV2CSConfigTab(NJ2CS instance)
	    {
	        baseInstance = instance;
	        stackPanel = new StackPanel { Margin = new Thickness(10) };
	        Content = stackPanel;
	
	        Loaded += (s, e) => LoadConfigContent();
	    }
	
	    public void LoadConfigContent()
	    {
	        if (baseInstance != null)
	        {
	            baseInstance.LoadConfigContent(stackPanel);
	        }
	        else
	        {
				NinjaTrader.Code.Output.Process($"Configuration instance is missing.", PrintTo.OutputTab1);
	        }
	    }
	
	    protected override string GetHeaderPart(string name)
	    {
	        return "Settings"; // Tab title
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
