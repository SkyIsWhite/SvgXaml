using System;
using System.ComponentModel.Composition;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Task = System.Threading.Tasks.Task;

namespace DrawingGroupViewer.Adornments
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType(ContentTypes.Xaml)]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    public sealed class SvgAdornmentProvider : IWpfTextViewCreationListener
    {
        public void TextViewCreated(IWpfTextView textView)
        {
            textView.Properties.GetOrCreateSingletonProperty(() => new SvgAdornment(textView));
        }
    }
}