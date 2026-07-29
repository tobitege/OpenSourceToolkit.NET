using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSourceToolkit.NET.ViewModels.Tools.ImageConverter;
using OpenSourceToolkit.NET.ViewModels.Tools.ImageConverter.Models;

namespace OpenSourceToolkit.Tests
{
    [TestClass]
    public class AiAssistantPanelTests
    {
        [TestMethod]
        public void AiPanel_HasNoWorkspaceImageEnablementGate()
        {
            var viewPath = FindViewPath("ImageConverterToolView.axaml");

            var document = XDocument.Load(viewPath);
            var xamlNamespace = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
            var panel = document
                .Descendants()
                .Single(element => (string)element.Attribute(xamlNamespace + "Name") == "RightPanelBorder");

            Assert.IsNull(
                panel.Attribute("IsEnabled"),
                "The AI panel must remain enabled so text chat and image generation work without a workspace image.");
        }

        [TestMethod]
        public void ImageToolbar_HasAlwaysEnabledAiSettingsButtonBetweenAiGenAndSessions()
        {
            var viewPath = FindViewPath("ImageConverterToolView.axaml");
            var document = XDocument.Load(viewPath);
            var aiSettingsButton = document
                .Descendants()
                .Single(element =>
                    element.Attributes().Any(attribute =>
                        attribute.Name.LocalName == "AutomationProperties.AutomationId" &&
                        attribute.Value == "ImageEditorAiSettingsButton"));
            var toolbarChildren = aiSettingsButton.Parent.Elements().ToList();
            var aiGenButton = toolbarChildren
                .Single(element => element
                    .Descendants()
                    .Any(descendant =>
                        descendant.Name.LocalName == "TextBlock" &&
                        (string)descendant.Attribute("Text") == "{loc:Localize Image_AIGen_Button}"));
            var sessionsButton = toolbarChildren
                .Single(element => element
                    .Descendants()
                    .Any(descendant =>
                        descendant.Name.LocalName == "TextBlock" &&
                        (string)descendant.Attribute("Text") == "{loc:Localize Image_Sessions_Button}"));

            Assert.AreEqual(toolbarChildren.IndexOf(aiGenButton) + 1, toolbarChildren.IndexOf(aiSettingsButton));
            Assert.AreEqual(toolbarChildren.IndexOf(aiSettingsButton) + 1, toolbarChildren.IndexOf(sessionsButton));
            Assert.AreEqual("{Binding Ai.HasAiAccess}", (string)aiGenButton.Attribute("IsEnabled"));
            Assert.AreEqual("{Binding Ai.AiButtonTooltip}", (string)aiGenButton.Attribute("ToolTip.Tip"));
            Assert.AreEqual("True", (string)aiGenButton.Attribute("ToolTip.ShowOnDisabled"));
            Assert.AreEqual("OnOpenAiSettingsClicked", (string)aiSettingsButton.Attribute("Click"));
            Assert.AreEqual("{loc:Localize Image_AISettings_Tooltip}", (string)aiSettingsButton.Attribute("ToolTip.Tip"));
            Assert.IsNotNull(aiSettingsButton.Attributes().SingleOrDefault(
                attribute => attribute.Name.LocalName == "AutomationProperties.Name"));
            Assert.AreEqual("DaisyButton", aiSettingsButton.Name.LocalName);
            Assert.AreEqual("True", (string)aiSettingsButton.Attribute("IsEnabled"));
            Assert.AreEqual("True", (string)aiSettingsButton.Attribute("IsHitTestVisible"));
            Assert.AreEqual("True", (string)aiSettingsButton.Attribute("Focusable"));
            Assert.AreEqual("Small", (string)aiSettingsButton.Attribute("Size"));
            Assert.AreEqual("Square", (string)aiSettingsButton.Attribute("Shape"));
            Assert.AreEqual("Ghost", (string)aiSettingsButton.Attribute("Variant"));
            Assert.AreEqual(
                "{StaticResource SettingsIcon}",
                (string)aiSettingsButton
                    .Descendants()
                    .Single(element => element.Name.LocalName == "PathIcon")
                    .Attribute("Data"));

            var codeBehindPath = FindViewPath("ImageConverterToolView.axaml.cs");
            var source = File.ReadAllText(codeBehindPath);
            StringAssert.Contains(
                source,
                "mainWindow.OpenSettings(global::OpenSourceToolkit.NET.Views.SettingsSection.AiConnections);");
        }

        [TestMethod]
        public void AiErrorMessage_IsWrappedScrollableSelectableAndActionable()
        {
            var viewPath = FindViewPath("ImageConverter", "AiAssistantPanel.axaml");
            var document = XDocument.Load(viewPath);
            var errorBubble = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "DaisyChatBubble" &&
                    (string)element.Attribute("Header") == "Error");

            var errorText = errorBubble
                .Descendants()
                .Single(element => element.Name.LocalName == "TextBox");
            var actions = errorBubble
                .Descendants()
                .Single(element => element.Name.LocalName == "ContentControl");

            Assert.AreEqual("{Binding Content}", (string)errorText.Attribute("Text"));
            Assert.AreEqual("True", (string)errorText.Attribute("IsReadOnly"));
            Assert.AreEqual("True", (string)errorText.Attribute("AcceptsReturn"));
            Assert.AreEqual("Wrap", (string)errorText.Attribute("TextWrapping"));
            Assert.AreEqual("Disabled", (string)errorText.Attribute("ScrollViewer.HorizontalScrollBarVisibility"));
            Assert.AreEqual("Auto", (string)errorText.Attribute("ScrollViewer.VerticalScrollBarVisibility"));
            Assert.AreEqual("{StaticResource ChatMessageActionsTemplate}", (string)actions.Attribute("ContentTemplate"));
        }

        [TestMethod]
        public void AiMessages_AreResponsiveAndExposeCopyAndDeleteActions()
        {
            var viewPath = FindViewPath("ImageConverter", "AiAssistantPanel.axaml");
            var document = XDocument.Load(viewPath);
            var xamlNamespace = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
            var servicesNamespace = XNamespace.Get("clr-namespace:Flowery.Services;assembly=Flowery.NET");

            var scrollViewer = document
                .Descendants()
                .Single(element => (string)element.Attribute(xamlNamespace + "Name") == "ChatScrollViewer");
            Assert.AreEqual(
                "True",
                (string)scrollViewer.Attribute(servicesNamespace + "FloweryResponsive.IsEnabled"));

            const string responsiveWidthBinding =
                "{Binding (services:FloweryResponsive.ResponsiveMaxWidth), RelativeSource={RelativeSource AncestorType=ScrollViewer}}";
            const string responsiveContentWidthBinding =
                "{Binding (services:FloweryResponsive.ResponsiveMaxWidth), RelativeSource={RelativeSource AncestorType=ScrollViewer}, Converter={StaticResource SubtractDoubleConverter}, ConverterParameter=32}";
            const string responsiveUserContentWidthBinding =
                "{Binding (services:FloweryResponsive.ResponsiveMaxWidth), RelativeSource={RelativeSource AncestorType=ScrollViewer}, Converter={StaticResource SubtractDoubleConverter}, ConverterParameter=16}";
            var bubbleBorderStyle = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "Style" &&
                    (string)element.Attribute("Selector") == "daisy|DaisyChatBubble /template/ Border#PART_Bubble");
            var maxWidthSetter = bubbleBorderStyle
                .Elements()
                .Single(element =>
                    element.Name.LocalName == "Setter" &&
                    (string)element.Attribute("Property") == "MaxWidth");
            var bubbleContentStyle = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "Style" &&
                    (string)element.Attribute("Selector") ==
                    "daisy|DaisyChatBubble /template/ Border#PART_Bubble > ContentPresenter");

            Assert.AreEqual(responsiveWidthBinding, (string)maxWidthSetter.Attribute("Value"));
            Assert.IsTrue(bubbleContentStyle
                .Elements()
                .Any(element =>
                    element.Name.LocalName == "Setter" &&
                    (string)element.Attribute("Property") == "MaxWidth" &&
                    (string)element.Attribute("Value") == responsiveContentWidthBinding));
            var userBubbleContentStyle = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "Style" &&
                    (string)element.Attribute("Selector") ==
                    "daisy|DaisyChatBubble.compact-user-message /template/ Border#PART_Bubble > ContentPresenter");
            Assert.IsTrue(userBubbleContentStyle
                .Elements()
                .Any(element =>
                    element.Name.LocalName == "Setter" &&
                    (string)element.Attribute("Property") == "MaxWidth" &&
                    (string)element.Attribute("Value") == responsiveUserContentWidthBinding));

            var bubbles = document
                .Descendants()
                .Where(element => element.Name.LocalName == "DaisyChatBubble")
                .ToList();

            Assert.AreEqual(5, bubbles.Count);
            Assert.IsTrue(bubbles.All(bubble => (string)bubble.Attribute("MaxWidth") == responsiveWidthBinding));
            var userBubble = bubbles.Single(bubble => (string)bubble.Attribute("IsEnd") == "True");
            Assert.AreEqual("compact-user-message", (string)userBubble.Attribute("Classes"));
            Assert.AreEqual("8", (string)userBubble.Attribute("Padding"));
            Assert.IsNull(userBubble.Attribute("Footer"));
            Assert.IsTrue(userBubble.Parent?
                .Elements()
                .Any(element =>
                    element.Name.LocalName == "TextBlock" &&
                    (string)element.Attribute("Text") == "{Binding Timestamp, StringFormat='{}{0:HH:mm}'}") == true);
            Assert.IsTrue(bubbles.All(bubble => bubble
                .Descendants()
                .Any(element =>
                    element.Name.LocalName == "ContentControl" &&
                    (string)element.Attribute("ContentTemplate") == "{StaticResource ChatMessageActionsTemplate}")));

            var actionsTemplate = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "DataTemplate" &&
                    (string)element.Attribute(xamlNamespace + "Key") == "ChatMessageActionsTemplate");
            var actionButtons = actionsTemplate
                .Descendants()
                .Where(element => element.Name.LocalName == "DaisyButton")
                .ToList();

            Assert.AreEqual(2, actionButtons.Count);
            Assert.IsTrue(actionButtons.Any(button => (string)button.Attribute("Click") == "OnCopyMessageClicked"));
            var deleteButton = actionButtons.Single(button => (string)button.Attribute("Click") == "OnDeleteMessageClicked");
            Assert.IsTrue(actionButtons.All(button => (string)button.Attribute("Variant") == "Ghost"));
            Assert.AreEqual(
                "{DynamicResource DaisyErrorBrush}",
                (string)deleteButton.Descendants().Single(element => element.Name.LocalName == "PathIcon").Attribute("Foreground"));
            Assert.IsTrue(actionButtons.All(button => (string)button.Attribute("Tag") == "{Binding}"));
        }

        [TestMethod]
        public void AiPanel_WiresMessageCopyToTopLevelClipboard()
        {
            var codeBehindPath = FindViewPath("ImageConverterToolView.axaml.cs");
            var source = File.ReadAllText(codeBehindPath);

            StringAssert.Contains(source, "protected override void OnDataContextChanged(EventArgs e)");
            StringAssert.Contains(source, "vm.CopyToClipboardAction = CopyTextToClipboardAsync;");
            StringAssert.Contains(source, "TopLevel.GetTopLevel(this)?.Clipboard");
            StringAssert.Contains(source, "await clipboard.SetTextAsync(text);");
            StringAssert.Contains(source, "await clipboard.FlushAsync();");
        }

        [TestMethod]
        public async Task CopyMessage_PassesCompleteErrorPayloadToClipboardAction()
        {
            const string payload = "{\"error\":{\"message\":\"Provider returned error\",\"code\":400}}";
            var viewModel = (AiAssistantViewModel)RuntimeHelpers.GetUninitializedObject(typeof(AiAssistantViewModel));
            string copiedText = null;
            viewModel.CopyToClipboardAction = text =>
            {
                copiedText = text;
                return Task.CompletedTask;
            };
            var copyMessage = typeof(AiAssistantViewModel).GetMethod(
                "CopyMessageToClipboardAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(copyMessage);
            var copyTask = (Task)copyMessage.Invoke(
                viewModel,
                new object[] { ChatMessageItem.System(payload, isError: true) });
            await copyTask;

            Assert.AreEqual(payload, copiedText);
        }

        [TestMethod]
        public async Task CopyAll_PassesFormattedChatLogToClipboardAction()
        {
            var viewModel = new AiAssistantViewModel();
            viewModel.ChatMessages.Add(ChatMessageItem.User("Create an image"));
            viewModel.ChatMessages.Add(ChatMessageItem.System("Provider returned error", isError: true));
            string copiedText = null;
            viewModel.CopyToClipboardAction = text =>
            {
                copiedText = text;
                return Task.CompletedTask;
            };

            await viewModel.AiChatCopyCommand.ExecuteAsync(null);

            StringAssert.Contains(copiedText, "[You]");
            StringAssert.Contains(copiedText, "Create an image");
            StringAssert.Contains(copiedText, "[System]");
            StringAssert.Contains(copiedText, "Provider returned error");
        }

        [TestMethod]
        public void DeleteMessage_RemovesOnlySelectedMessageAndNotifiesChatChange()
        {
            var viewModel = new AiAssistantViewModel();
            var first = ChatMessageItem.User("Delete me");
            var second = ChatMessageItem.Assistant("Keep me");
            var changeCount = 0;
            viewModel.ChatMessages.Add(first);
            viewModel.ChatMessages.Add(second);
            viewModel.OnChatChanged = () => changeCount++;

            viewModel.DeleteMessageCommand.Execute(first);

            Assert.AreEqual(1, viewModel.ChatMessages.Count);
            Assert.AreSame(second, viewModel.ChatMessages[0]);
            Assert.AreEqual(1, changeCount);
            Assert.IsTrue(viewModel.HasMessages);
        }

        [TestMethod]
        public void AiPanel_HasSettingsButtonWithCogIcon()
        {
            var viewPath = FindViewPath("ImageConverter", "AiAssistantPanel.axaml");
            var document = XDocument.Load(viewPath);
            var settingsButton = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "DaisyButton" &&
                    (string)element.Attribute("Click") == "OnOpenSettingsClicked");
            var icon = settingsButton
                .Descendants()
                .Single(element => element.Name.LocalName == "PathIcon");

            Assert.AreEqual("Open Settings", (string)settingsButton.Attribute("ToolTip.Tip"));
            Assert.AreEqual("{StaticResource SettingsIcon}", (string)icon.Attribute("Data"));

            var codeBehind = File.ReadAllText(viewPath + ".cs");
            StringAssert.Contains(
                codeBehind,
                "mainWindow.OpenSettings(global::OpenSourceToolkit.NET.Views.SettingsSection.AiProviders);");
        }

        [TestMethod]
        public void AiPanel_UsesConfiguredAccessWithoutAuthenticationControls()
        {
            var viewPath = FindViewPath("ImageConverter", "AiAssistantPanel.axaml");
            var document = XDocument.Load(viewPath);

            var apiConnection = document
                .Descendants()
                .Single(element => element.Attributes().Any(attribute =>
                    attribute.Name.LocalName == "AutomationProperties.AutomationId" &&
                    attribute.Value == "AiApiConnection"));
            var imageSettings = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "StackPanel" &&
                    (string)element.Attribute("IsVisible") == "{Binding IsImageGenerationConnection}");
            var sendImage = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "DaisyCheckBox" &&
                    (string)element.Attribute("Content") == "Send current image?");
            var codeBehind = File.ReadAllText(viewPath + ".cs");
            var viewModelSource = File.ReadAllText(FindViewModelPath());

            Assert.AreEqual("{Binding AiConnectionNames}", (string)apiConnection.Attribute("ItemsSource"));
            Assert.IsNull(
                apiConnection.Parent.Attribute("IsVisible"),
                "Configured AI connections must remain selectable in every access mode.");
            Assert.AreEqual("{Binding IsImageGenerationConnection}", (string)imageSettings.Attribute("IsVisible"));
            Assert.AreEqual("{Binding IsApiMode}", (string)sendImage.Attribute("IsVisible"));
            Assert.IsFalse(document
                .Descendants()
                .Any(element => element.Attributes().Any(attribute =>
                    attribute.Name.LocalName == "AutomationProperties.AutomationId" &&
                    (attribute.Value == "AiAuthenticationMode" ||
                     attribute.Value == "AiSubscriptionModel"))));
            Assert.IsFalse(codeBehind.Contains("OpenBrowserAsync", StringComparison.Ordinal));
            Assert.IsFalse(codeBehind.Contains("Launcher", StringComparison.Ordinal));
            Assert.IsFalse(viewModelSource.Contains("OpenBrowserAction", StringComparison.Ordinal));
            Assert.IsFalse(viewModelSource.Contains("LoginSubscriptionCommand", StringComparison.Ordinal));
            Assert.IsFalse(viewModelSource.Contains("ChangeAccessModeAsync", StringComparison.Ordinal));
            StringAssert.Contains(
                viewModelSource,
                "_currentConfig?.ProviderType == AiProviderType.Codex");
            StringAssert.Contains(
                viewModelSource,
                "TrySelectConfiguredSubscriptionModel()");
            StringAssert.Contains(
                viewModelSource,
                "_currentConfig.ModelId");
        }

        [TestMethod]
        public void OpenAICompatibleConnection_UsesCustomLlmProvider()
        {
            var mapProvider = typeof(AiAssistantViewModel).GetMethod(
                "MapToLlmProvider",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(mapProvider);
            var provider = mapProvider.Invoke(
                null,
                new object[]
                {
                    OpenSourceToolkit.NET.Services.Ai.AiProviderType.OpenAICompatible
                });

            Assert.IsNotNull(provider);
            Assert.AreEqual("Custom", provider.ToString());
        }

        private static string FindViewPath(params string[] relativePath)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null)
            {
                var pathParts = new[]
                {
                    directory.FullName,
                    "OpenSourceToolkit.NET",
                    "Views",
                    "Tools"
                }.Concat(relativePath).ToArray();
                var candidate = Path.Combine(pathParts);

                if (File.Exists(candidate))
                    return candidate;

                directory = directory.Parent;
            }

            Assert.Fail($"Could not locate {Path.Combine(relativePath)} from the test output directory.");
            return null;
        }

        private static string FindViewModelPath()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null)
            {
                var candidate = Path.Combine(
                    directory.FullName,
                    "OpenSourceToolkit.NET",
                    "ViewModels",
                    "Tools",
                    "ImageConverter",
                    "AiAssistantViewModel.cs");

                if (File.Exists(candidate))
                    return candidate;

                directory = directory.Parent;
            }

            Assert.Fail("Could not locate AiAssistantViewModel.cs from the test output directory.");
            return null;
        }
    }
}
