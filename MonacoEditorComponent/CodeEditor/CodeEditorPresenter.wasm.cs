﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Uno.Foundation;
using Uno.Foundation.Interop;

namespace Monaco
{
    public partial class CodeEditorPresenter : Control, ICodeEditorPresenter, IJSObject
	{
		private static readonly string UNO_BOOTSTRAP_APP_BASE = global::System.Environment.GetEnvironmentVariable(nameof(UNO_BOOTSTRAP_APP_BASE));

		private readonly JSObjectHandle _handle;

		/// <inheritdoc />
		JSObjectHandle IJSObject.Handle => _handle;

		public CodeEditorPresenter() : base("iframe")
		{
			//Background = new SolidColorBrush(Colors.Red);
			RaiseDOMContentLoaded();

			_handle = JSObjectHandle.Create(this);
			WebAssemblyRuntime.InvokeJSWithInterop($@"
				console.log(""///////////////////////////////// subscribing to DOMContentLoaded - "" + {HtmlId});

				var frame = Uno.UI.WindowManager.current.getView({HtmlId});
				
				console.log(""Got view"");

				frame.addEventListener(""loadstart"", function(event) {{
					var frameDoc = frame.contentDocument;
					console.log(""/////////////////////////////////  Frame DOMContentLoaded, subscribing to document"" + frameDoc);
					{this}.RaiseDOMContentLoaded();
				}}); 
				console.log(""Added load start"");



				frame.addEventListener(""load"", function(event) {{
					var frameDoc = frame.contentDocument;
					console.log(""/////////////////////////////////  Frame loaded, subscribing to document"" + frameDoc);
					{this}.RaiseDOMContentLoaded();
					//frameDoc.addEventListener(""DOMContentLoaded"", function(event) {{
					//	console.log(""Raising RaiseDOMContentLoaded"");
					//	{this}.RaiseDOMContentLoaded();
					//}});
				}}); 

				console.log(""Added load"");


				");
		}

		public void RaiseDOMContentLoaded()
		{
			Console.Error.WriteLine($"Handle is null {_handle == null}");
			if (_handle == null) return;

			Console.Error.WriteLine("-------------------------------------------------------- RaiseDOMContentLoaded");
			Dispatcher.RunAsync(CoreDispatcherPriority.Low, () => DOMContentLoaded?.Invoke(null, new WebViewDOMContentLoadedEventArgs()));
		}

		/// <inheritdoc />
		protected override void OnLoaded()
		{
			base.OnLoaded();

			/*Console.Error.WriteLine("---------------------- LOADED ");


			var script = $@"
					var frame = Uno.UI.WindowManager.current.getView({HtmlId});
					var frameDoc = frame.contentDocument;
					
					return frameDoc.onload = function() { };

			Console.Error.WriteLine("***************************************** AddWebAllowedObject: " + script);*/
		}

		/// <inheritdoc />
		public void AddWebAllowedObject(string name, object pObject)
		{
			if (pObject is IJSObject obj)
			{
				Console.Error.WriteLine($"Add Web Allowed Object - {name}");
				var method = obj.Handle.GetType().GetMethod("GetNativeInstance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				Console.Error.WriteLine($"*** Method exists {method != null}");
				var native  = method.Invoke(obj.Handle,new object[] { }) as string;
				Console.Error.WriteLine($"*** Native handle {native}");


				var script = $@"
					var value = {native};
					var frame = Uno.UI.WindowManager.current.getView({HtmlId});
					var frameWindow = frame.contentWindow;
					
					console.log(value);

					frameWindow.{name} = value;
					";
                ////frameWindow.eval(""var {name} = window.parent.{obj.Handle.GetNativeInstance().Replace("\"", "\\\"")}; ""); 

                Console.Error.WriteLine("***************************************** AddWebAllowedObject: " + script);

                try
                {
                    WebAssemblyRuntime.InvokeJS(script);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine("FAILED " + e);
                }
            }
			else
			{
				Console.Error.WriteLine(name + " is not a JSObject :( :( :( :( :( :( :( :( :( :( :( :( :( :( :( :( :( :( :( :( ");
			}
		}

		/// <inheritdoc />
		public event TypedEventHandler<ICodeEditorPresenter, WebViewNewWindowRequestedEventArgs> NewWindowRequested; // ignored for now (external navigation)

		/// <inheritdoc />
		public event TypedEventHandler<ICodeEditorPresenter, WebViewNavigationStartingEventArgs> NavigationStarting;

		/// <inheritdoc />
		public event TypedEventHandler<ICodeEditorPresenter, WebViewDOMContentLoadedEventArgs> DOMContentLoaded;

		/// <inheritdoc />
		public event TypedEventHandler<ICodeEditorPresenter, WebViewNavigationCompletedEventArgs> NavigationCompleted; // ignored for now (only focus the editor)

		/// <inheritdoc />
		public global::System.Uri Source
		{
			get => new global::System.Uri(GetAttribute("src"));
			set
			{
                //var path = Environment.GetEnvironmentVariable("UNO_BOOTSTRAP_APP_BASE");
                //var target = $"/{path}/MonacoCodeEditor.html";
                //var target = (value.IsAbsoluteUri && value.IsFile)
                //	? value.PathAndQuery 
                //	: value.ToString();

                string target;
				if (value.IsAbsoluteUri)
				{
					if(value.Scheme=="file")
					{
						// Local files are assumed as coming from the remoter server
						target = UNO_BOOTSTRAP_APP_BASE == null ? value.PathAndQuery : UNO_BOOTSTRAP_APP_BASE + value.PathAndQuery;
					}
                    else
                    {
						target = value.AbsoluteUri;

					}

				}
				else
				{
					target = UNO_BOOTSTRAP_APP_BASE == null
						? value.OriginalString
						: UNO_BOOTSTRAP_APP_BASE + "/" + value.OriginalString;
				}

				Console.Error.WriteLine("***** LOADING: " + target);

				Console.Error.WriteLine($"---- Nav is null {NavigationStarting == null}");

				SetAttribute("src", target);

				//NavigationStarting?.Invoke(this, new WebViewNavigationStartingEventArgs());
				Dispatcher.RunAsync(CoreDispatcherPriority.Low, () => NavigationStarting?.Invoke(this, new WebViewNavigationStartingEventArgs()));

			}
		}

		/// <inheritdoc />
		public IAsyncOperation<string> InvokeScriptAsync(string scriptName, IEnumerable<string> arguments)
		{
			Console.WriteLine("+++++++++++++++++++++++++++++++ Invoke Script +++++++++++++++++++++++++++++++++++++++++++++++++++++++");
			var script = $@"(function() {{
				var frame = Uno.UI.WindowManager.current.getView({HtmlId});
				var frameWindow = frame.contentWindow;
				
				try {{
					frameWindow.__evalMethod = function() {{ {arguments.Single()} }};
					
					return frameWindow.eval(""__evalMethod()"") || """";
				}}
				finally {{
					frameWindow.__evalMethod = null;
				}}
			}})()";
			Console.Error.WriteLine(script);

			try
			{
				var result = WebAssemblyRuntime.InvokeJS(script);

				Console.WriteLine("++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++");
				Console.WriteLine(result);

				return Task.FromResult(result).AsAsyncOperation();
			}
			catch (Exception e)
			{
				Console.WriteLine("++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++");
				Console.WriteLine(e);

				return Task.FromResult("").AsAsyncOperation();
			}
		}
	}
}