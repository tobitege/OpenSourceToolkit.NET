using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSourceToolkit.NET.Data;
using OpenSourceToolkit.NET.ViewModels;
using OpenSourceToolkit.NET.Views;

namespace OpenSourceToolkit.Tests
{
    [TestClass]
    public class SettingsConnectionTests
    {
        [TestMethod]
        public void ConnectionDisplayText_NotifiesWhenItsPartsChange()
        {
            var connection = new AiConnectionViewModel
            {
                Name = "Connection",
                ProviderType = "OpenRouter",
                ModelId = "model-1"
            };
            var changedProperties = new List<string>();
            connection.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

            connection.Name = "Renamed";
            connection.ProviderType = "OpenAI";
            connection.ModelId = "model-2";

            Assert.AreEqual("Renamed (OpenAI: model-2)", connection.DisplayText);
            Assert.AreEqual(3, changedProperties.FindAll(name => name == nameof(connection.DisplayText)).Count);
        }

        [TestMethod]
        public void SelectingKnownModel_UpdatesImageGenerationCapability()
        {
            var viewModel = (SettingsViewModel)RuntimeHelpers.GetUninitializedObject(typeof(SettingsViewModel));
            SetField(viewModel, "_editSelectedProvider", "OpenRouter");
            SetField(viewModel, "_editSupportsImageGeneration", false);
            viewModel.EditAvailableModelOptions = new List<AiModelOption>
            {
                new AiModelOption("google/gemini-3.1-flash-lite-image", true),
                new AiModelOption("anthropic/claude-sonnet-4.5", false)
            };

            viewModel.EditSelectedModel = "google/gemini-3.1-flash-lite-image";

            Assert.IsTrue(viewModel.EditSupportsImageGeneration);
            Assert.IsTrue(viewModel.IsEditImageGenerationCapabilityDetected);
            Assert.IsFalse(viewModel.CanEditImageGenerationCapability);

            viewModel.EditSelectedModel = "anthropic/claude-sonnet-4.5";

            Assert.IsFalse(viewModel.EditSupportsImageGeneration);
            Assert.IsFalse(viewModel.IsEditImageGenerationCapabilityDetected);
            Assert.IsTrue(viewModel.CanEditImageGenerationCapability);

            viewModel.EditSupportsImageGeneration = true;
            viewModel.EditSelectedModel = "anthropic/claude-sonnet-4.5";

            Assert.IsTrue(viewModel.EditSupportsImageGeneration);
        }

        [TestMethod]
        public void SaveConnection_CompletesWithSavedSelectionStillOpen()
        {
            var viewModel = (SettingsViewModel)RuntimeHelpers.GetUninitializedObject(typeof(SettingsViewModel));
            var savedConnection = new AiConnectionViewModel
            {
                Id = "connection-1",
                Name = "Connection",
                ProviderType = "OpenRouter",
                ModelId = "model-1"
            };
            var completeSave = typeof(SettingsViewModel).GetMethod(
                "CompleteConnectionSave",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(completeSave);
            completeSave.Invoke(viewModel, new object[] { savedConnection });

            Assert.AreSame(savedConnection, viewModel.SelectedConnection);
            Assert.IsTrue(viewModel.IsEditingConnection);
            Assert.IsFalse(viewModel.IsAddingConnection);
            Assert.IsFalse(viewModel.HasUnsavedConnectionChanges);
        }

        [TestMethod]
        public void SaveConnectionCommand_TracksConnectionEdits()
        {
            var viewModel = (SettingsViewModel)RuntimeHelpers.GetUninitializedObject(typeof(SettingsViewModel));
            SetField(viewModel, "_isEditingConnection", true);
            SetField(viewModel, "_editConnectionName", "Connection");
            SetField(viewModel, "_originalConnectionName", "Connection");

            var command = new RelayCommand(() => { }, () => viewModel.HasUnsavedConnectionChanges);
            SetField(viewModel, "<SaveConnectionCommand>k__BackingField", command);
            var notifications = 0;
            command.CanExecuteChanged += (_, _) => notifications++;

            Assert.IsFalse(command.CanExecute(null));

            viewModel.EditConnectionName = "Renamed";

            Assert.IsTrue(command.CanExecute(null));
            Assert.IsTrue(notifications > 0);

            viewModel.EditConnectionName = "Connection";

            Assert.IsFalse(command.CanExecute(null));
        }

        [TestMethod]
        public async System.Threading.Tasks.Task CanCloseAsync_DiscardKeepsSettingsOpenAndClearsConnectionEditState()
        {
            var viewModel = (SettingsViewModel)RuntimeHelpers.GetUninitializedObject(typeof(SettingsViewModel));
            SetField(viewModel, "_isEditingConnection", true);
            SetField(viewModel, "_isAddingConnection", true);
            SetField(viewModel, "_editConnectionName", "Codex");
            SetField(viewModel, "_editSelectedProvider", "OpenAI");
            viewModel.PromptSaveChangesAction =
                _ => System.Threading.Tasks.Task.FromResult<bool?>(false);

            Assert.IsTrue(viewModel.HasUnsavedConnectionChanges);

            Assert.IsFalse(await viewModel.CanCloseAsync());
            Assert.IsFalse(viewModel.HasUnsavedConnectionChanges);
            Assert.IsFalse(viewModel.IsEditingConnection);
            Assert.IsFalse(viewModel.IsAddingConnection);
        }

        [TestMethod]
        public void NewConnectionDefaults_AreNotDirtyUntilUserChangesAValue()
        {
            var viewModel = (SettingsViewModel)RuntimeHelpers.GetUninitializedObject(typeof(SettingsViewModel));
            SetField(viewModel, "_isEditingConnection", true);
            SetField(viewModel, "_isAddingConnection", true);
            SetField(viewModel, "_editConnectionName", "");
            SetField(viewModel, "_editSelectedProvider", "OpenAI");
            SetField(viewModel, "_editSelectedModel", "gpt-5.1");
            SetField(viewModel, "_editMaxTokens", 4096);
            SetField(viewModel, "_editTemperature", 0.7);
            SetField(viewModel, "_editSupportsMultiModal", true);
            SetField(viewModel, "_originalConnectionName", "");
            SetField(viewModel, "_originalProvider", "OpenAI");
            SetField(viewModel, "_originalModel", "gpt-5.1");
            SetField(viewModel, "_originalMaxTokens", 4096);
            SetField(viewModel, "_originalTemperature", 0.7);
            SetField(viewModel, "_originalSupportsMultiModal", true);

            Assert.IsFalse(viewModel.HasUnsavedConnectionChanges);

            viewModel.EditConnectionName = "Codex";

            Assert.IsTrue(viewModel.HasUnsavedConnectionChanges);
        }

        [TestMethod]
        public async System.Threading.Tasks.Task CancelConnectionEditAsync_WithUserChangesPromptsBeforeDiscard()
        {
            var viewModel = (SettingsViewModel)RuntimeHelpers.GetUninitializedObject(typeof(SettingsViewModel));
            SetField(viewModel, "_isEditingConnection", true);
            SetField(viewModel, "_isAddingConnection", true);
            SetField(viewModel, "_editConnectionName", "Codex");
            SetField(viewModel, "_originalConnectionName", "");
            var promptCount = 0;
            viewModel.PromptSaveChangesAction = _ =>
            {
                promptCount++;
                return System.Threading.Tasks.Task.FromResult<bool?>(false);
            };
            var cancel = typeof(SettingsViewModel).GetMethod(
                "CancelConnectionEditAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(cancel);
            var cancelTask = cancel.Invoke(viewModel, null) as System.Threading.Tasks.Task;
            Assert.IsNotNull(cancelTask);
            await cancelTask;

            Assert.AreEqual(1, promptCount);
            Assert.IsFalse(viewModel.HasUnsavedConnectionChanges);
            Assert.IsFalse(viewModel.IsEditingConnection);
            Assert.IsFalse(viewModel.IsAddingConnection);
        }

        [TestMethod]
        public async System.Threading.Tasks.Task CancelConnectionEditAsync_WithoutUserChangesDoesNotPrompt()
        {
            var viewModel = (SettingsViewModel)RuntimeHelpers.GetUninitializedObject(typeof(SettingsViewModel));
            SetField(viewModel, "_isEditingConnection", true);
            SetField(viewModel, "_isAddingConnection", true);
            SetField(viewModel, "_editConnectionName", "");
            SetField(viewModel, "_editSelectedProvider", "OpenAI");
            SetField(viewModel, "_editSelectedModel", "gpt-5.1");
            SetField(viewModel, "_editMaxTokens", 4096);
            SetField(viewModel, "_editTemperature", 0.7);
            SetField(viewModel, "_editSupportsMultiModal", true);
            SetField(viewModel, "_originalConnectionName", "");
            SetField(viewModel, "_originalProvider", "OpenAI");
            SetField(viewModel, "_originalModel", "gpt-5.1");
            SetField(viewModel, "_originalMaxTokens", 4096);
            SetField(viewModel, "_originalTemperature", 0.7);
            SetField(viewModel, "_originalSupportsMultiModal", true);
            var promptCount = 0;
            viewModel.PromptSaveChangesAction = _ =>
            {
                promptCount++;
                return System.Threading.Tasks.Task.FromResult<bool?>(false);
            };
            var cancel = typeof(SettingsViewModel).GetMethod(
                "CancelConnectionEditAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(cancel);
            var cancelTask = cancel.Invoke(viewModel, null) as System.Threading.Tasks.Task;
            Assert.IsNotNull(cancelTask);
            await cancelTask;

            Assert.AreEqual(0, promptCount);
            Assert.IsFalse(viewModel.IsEditingConnection);
            Assert.IsFalse(viewModel.IsAddingConnection);
        }

        [TestMethod]
        public void ConnectionEditor_ExposesTestConnectionActionAndStatus()
        {
            var document = XDocument.Load(FindSettingsViewPath());
            var testButton = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "DaisyButton" &&
                    (string)element.Attribute("Command") == "{Binding TestConnectionCommand}");
            var status = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "TextBlock" &&
                    (string)element.Attribute("Text") == "{Binding ConnectionTestStatus}");

            Assert.AreEqual("{Binding CanTestConnection}", (string)testButton.Attribute("IsEnabled"));
            Assert.IsNotNull(status.Attribute("IsVisible"));
        }

        [TestMethod]
        public void ConnectionEditor_AlignsTestActionWithModelColumn()
        {
            var document = XDocument.Load(FindSettingsViewPath());
            var testButton = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "DaisyButton" &&
                    (string)element.Attribute("Command") == "{Binding TestConnectionCommand}");
            var actionRow = testButton.Parent;

            Assert.AreEqual("Grid", actionRow.Name.LocalName);
            Assert.AreEqual("1", (string)actionRow.Attribute("Grid.Column"));
            Assert.AreEqual("Auto,*,Auto", (string)actionRow.Attribute("ColumnDefinitions"));
            Assert.AreEqual("0", (string)testButton.Attribute("Grid.Column"));
        }

        [TestMethod]
        public void ConnectionEditor_ExposesCancelActionNextToSave()
        {
            var document = XDocument.Load(FindSettingsViewPath());
            var cancelButton = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "DaisyButton" &&
                    (string)element.Attribute("Command") == "{Binding CancelConnectionCommand}");
            var saveButton = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "DaisyButton" &&
                    (string)element.Attribute("Command") == "{Binding SaveConnectionCommand}");
            var actionButtons = cancelButton.Parent
                .Elements()
                .Where(element => element.Name.LocalName == "DaisyButton")
                .ToList();

            Assert.AreSame(cancelButton.Parent, saveButton.Parent);
            Assert.AreEqual(actionButtons.IndexOf(saveButton) + 1, actionButtons.IndexOf(cancelButton));
            Assert.AreEqual("{loc:Localize Button_Cancel}", (string)cancelButton.Attribute("Content"));
            Assert.AreEqual("Error", (string)cancelButton.Attribute("Variant"));
        }

        [TestMethod]
        public void ConnectionEditor_OffersCodexSeparatelyFromOpenAiApiProviders()
        {
            CollectionAssert.Contains(
                OpenSourceToolkit.NET.Services.Ai.AiSettingsManager.SupportedConnectionProviders,
                "Codex");
            CollectionAssert.DoesNotContain(
                OpenSourceToolkit.NET.Services.Ai.AiSettingsManager.SupportedProviders,
                "Codex");

            var document = XDocument.Load(FindSettingsViewPath());
            var providerSelect = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "DaisySelect" &&
                    (string)element.Attribute("SelectedItem") == "{Binding EditSelectedProvider}");

            Assert.AreEqual(
                "{Binding ConnectionProviders}",
                (string)providerSelect.Attribute("ItemsSource"));
        }

        [TestMethod]
        public void ConnectionEditor_OffersOpenAICompatibleWithPerConnectionBaseUrl()
        {
            var document = XDocument.Load(FindSettingsViewPath());
            var baseUrlInput = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "DaisyInput" &&
                    (string)element.Attribute("Text") == "{Binding EditCustomEndpoint}");

            Assert.AreEqual(
                "{Binding IsEditingOpenAICompatibleConnection}",
                (string)baseUrlInput.Attribute("IsVisible"));
            CollectionAssert.Contains(
                OpenSourceToolkit.NET.Services.Ai.AiSettingsManager.SupportedConnectionProviders,
                "OpenAI-Compatible");
            CollectionAssert.DoesNotContain(
                OpenSourceToolkit.NET.Services.Ai.AiSettingsManager.SupportedProviders,
                "OpenAI-Compatible");
        }

        [TestMethod]
        public void OpenAICompatibleConnection_TestRequiresBaseUrlButNotApiKey()
        {
            var viewModel = (SettingsViewModel)RuntimeHelpers.GetUninitializedObject(typeof(SettingsViewModel));
            SetField(viewModel, "_editSelectedProvider", "OpenAI-Compatible");
            SetField(viewModel, "_editSelectedModel", "custom-model");
            SetField(viewModel, "_editCustomEndpoint", "http://localhost:8080/v1");

            Assert.IsTrue(viewModel.IsEditingOpenAICompatibleConnection);
            Assert.IsTrue(viewModel.CanTestConnection);

            SetField(viewModel, "_editCustomEndpoint", "not a URL");

            Assert.IsFalse(viewModel.CanTestConnection);
        }

        [TestMethod]
        public void ConnectionEditor_TestActionRequiresApiKeyAndHidesForSignedOutCodex()
        {
            var document = XDocument.Load(FindSettingsViewPath());
            var testButton = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "DaisyButton" &&
                    (string)element.Attribute("Command") == "{Binding TestConnectionCommand}");

            Assert.AreEqual(
                "{Binding ShowTestConnectionAction}",
                (string)testButton.Attribute("IsVisible"));
            Assert.AreEqual(
                "{Binding CanTestConnection}",
                (string)testButton.Attribute("IsEnabled"));

            var viewModelSource = File.ReadAllText(FindSettingsViewModelPath());
            StringAssert.Contains(
                viewModelSource,
                "IsEditingApiConnection || IsOpenAiSubscriptionAuthenticated");
            StringAssert.Contains(
                viewModelSource,
                "GetEffectiveConnectionTestApiKey()");
            StringAssert.Contains(
                viewModelSource,
                "aiManager.GetEffectiveApiKey(SelectedConnection.Id)");
        }

        [TestMethod]
        public void ConnectionEditor_HidesApiOnlyFieldsForCodex()
        {
            var document = XDocument.Load(FindSettingsViewPath());
            var apiOnlyElements = document
                .Descendants()
                .Where(element =>
                    (string)element.Attribute("IsVisible") == "{Binding IsEditingApiConnection}")
                .ToList();
            var viewModelSource = File.ReadAllText(FindSettingsViewModelPath());

            Assert.IsTrue(apiOnlyElements.Count >= 8);
            StringAssert.Contains(
                viewModelSource,
                "_aiAccessManager.SubscriptionModels");
            StringAssert.Contains(
                viewModelSource,
                "Select a Codex authentication mode in AI Providers first.");
        }

        [TestMethod]
        public void ConnectionEditor_ModelPickerIsSearchableCategorizedAndHeightLimited()
        {
            var document = XDocument.Load(FindSettingsViewPath());
            var xamlNamespace = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
            var modelPicker = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "AutoCompleteBox" &&
                    (string)element.Attribute("ItemsSource") == "{Binding EditAvailableModelOptions}");

            Assert.AreEqual("ConnectionModelPicker", (string)modelPicker.Attribute(xamlNamespace + "Name"));
            Assert.AreEqual("{Binding EditSelectedModel, Mode=TwoWay}", (string)modelPicker.Attribute("Text"));
            Assert.AreEqual("{Binding ModelId}", (string)modelPicker.Attribute("ValueMemberBinding"));
            Assert.AreEqual("0", (string)modelPicker.Attribute("MinimumPrefixLength"));
            Assert.AreEqual("0", (string)modelPicker.Attribute("MinimumPopulateDelay"));
            Assert.AreEqual("600", (string)modelPicker.Attribute("MaxDropDownHeight"));
            Assert.AreEqual("False", (string)modelPicker.Attribute("ClearSelectionOnLostFocus"));
            Assert.IsNull(modelPicker.Attribute("GotFocus"));
            Assert.IsNull(modelPicker.Attribute("SelectedItem"));

            var popupStyle = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "Style" &&
                    (string)element.Attribute("Selector") ==
                    "AutoCompleteBox#ConnectionModelPicker /template/ Popup#PART_Popup");
            var popupDismissSetter = popupStyle
                .Elements()
                .Single(element =>
                    element.Name.LocalName == "Setter" &&
                    (string)element.Attribute("Property") == "OverlayDismissEventPassThrough");
            Assert.AreEqual("True", (string)popupDismissSetter.Attribute("Value"));

            var imageGenerationCapability = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "DaisyCheckBox" &&
                    (string)element.Attribute("IsChecked") ==
                    "{Binding EditSupportsImageGeneration}");
            Assert.AreEqual(
                "{Binding CanEditImageGenerationCapability}",
                (string)imageGenerationCapability.Attribute("IsEnabled"));

            var itemTemplate = modelPicker
                .Descendants()
                .Single(element => element.Name.LocalName == "DataTemplate");
            var itemRoot = itemTemplate.Elements().Single();
            Assert.AreEqual("30", (string)itemRoot.Attribute("Height"));
            Assert.AreEqual(
                20,
                int.Parse((string)modelPicker.Attribute("MaxDropDownHeight")) /
                int.Parse((string)itemRoot.Attribute("Height")));
            Assert.IsTrue(itemTemplate
                .Descendants()
                .Any(element =>
                    element.Name.LocalName == "Border" &&
                    (string)element.Attribute("IsVisible") == "{Binding IsImageGeneration}"));
            Assert.IsTrue(itemTemplate
                .Descendants()
                .Any(element =>
                    element.Name.LocalName == "Border" &&
                    (string)element.Attribute("IsVisible") == "{Binding IsTextOnly}"));

            var openListButton = modelPicker.Parent
                .Elements()
                .Single(element =>
                    element.Name.LocalName == "DaisyButton" &&
                    (string)element.Attribute("Click") == "OpenConnectionModelList_Click");
            Assert.AreEqual("Default", (string)openListButton.Attribute("Variant"));

            var filterMethod = typeof(SettingsWindow).GetMethod(
                "FilterModelOption",
                BindingFlags.Static | BindingFlags.NonPublic);
            var imageModel = new AiModelOption("black-forest-labs/FLUX.2-pro", true);

            Assert.IsNotNull(filterMethod);
            Assert.AreEqual(true, filterMethod.Invoke(null, new object[] { "flux", imageModel }));
            Assert.AreEqual(false, filterMethod.Invoke(null, new object[] { "gpt", imageModel }));
            Assert.AreEqual(true, filterMethod.Invoke(null, new object[] { "", imageModel }));
        }

        [TestMethod]
        public void ConnectionEditor_EmptyRequiredNameUsesErrorVariant()
        {
            var viewModel = (SettingsViewModel)RuntimeHelpers.GetUninitializedObject(typeof(SettingsViewModel));
            SetField(viewModel, "_isEditingConnection", true);
            SetField(viewModel, "_editConnectionName", " ");

            Assert.IsTrue(viewModel.IsConnectionNameMissing);

            viewModel.EditConnectionName = "Named connection";

            Assert.IsFalse(viewModel.IsConnectionNameMissing);

            var document = XDocument.Load(FindSettingsViewPath());
            var nameInput = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "DaisyInput" &&
                    (string)element.Attribute("Text") == "{Binding EditConnectionName}");
            var requiredErrorStyle = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "Style" &&
                    (string)element.Attribute("Selector") == "daisy|DaisyInput.required-error");

            Assert.AreEqual(
                "{Binding IsConnectionNameMissing}",
                (string)nameInput.Attribute("Classes.required-error"));
            AssertStyleSetter(requiredErrorStyle, "Variant", "Error");
            AssertStyleSetter(requiredErrorStyle, "BorderBrush", "{DynamicResource DaisyErrorBrush}");
        }

        [TestMethod]
        public void ProviderEditor_SeparatesTextAndImageModelsWithIndependentSearches()
        {
            var document = XDocument.Load(FindSettingsViewPath());
            var textModels = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "ListBox" &&
                    (string)element.Attribute("ItemsSource") == "{Binding TextProviderModels}");
            var imageModels = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "ListBox" &&
                    (string)element.Attribute("ItemsSource") == "{Binding ImageProviderModels}");
            var textSearch = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "DaisyInput" &&
                    (string)element.Attribute("Text") == "{Binding TextModelSearchQuery}");
            var imageSearch = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "DaisyInput" &&
                    (string)element.Attribute("Text") == "{Binding ImageModelSearchQuery}");
            var textSearchAutomationId = textSearch
                .Attributes()
                .Single(attribute => attribute.Name.LocalName == "AutomationProperties.AutomationId")
                .Value;
            var imageSearchAutomationId = imageSearch
                .Attributes()
                .Single(attribute => attribute.Name.LocalName == "AutomationProperties.AutomationId")
                .Value;

            Assert.AreNotEqual(textSearchAutomationId, imageSearchAutomationId);
            Assert.AreEqual("TextProviderModels", textModels.Attributes().Single(attribute => attribute.Name.LocalName == "AutomationProperties.AutomationId").Value);
            Assert.AreEqual("ImageProviderModels", imageModels.Attributes().Single(attribute => attribute.Name.LocalName == "AutomationProperties.AutomationId").Value);
        }

        [TestMethod]
        public void OpenAiProviderEditor_ExposesApiAndChatGptSubscriptionModes()
        {
            var document = XDocument.Load(FindSettingsViewPath());
            var authenticationMode = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "ComboBox" &&
                    (string)element.Attribute("AutomationProperties.AutomationId") ==
                    "OpenAiAuthenticationMode");
            var apiConfiguration = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "StackPanel" &&
                    (string)element.Attribute("IsVisible") ==
                    "{Binding ShowProviderApiConfiguration}");
            var subscriptionConfiguration = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "DaisyCard" &&
                    (string)element.Attribute("IsVisible") ==
                    "{Binding ShowOpenAiSubscriptionConfiguration}");
            var subscriptionModel = subscriptionConfiguration
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "ComboBox" &&
                    (string)element.Attribute("AutomationProperties.AutomationId") ==
                    "OpenAiCodexModel");
            var subscriptionStatus = subscriptionConfiguration
                .Descendants()
                .Single(element =>
                    (string)element.Attribute("AutomationProperties.AutomationId") ==
                    "OpenAiCodexStatus");
            var reasoningEffort = subscriptionConfiguration
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "ComboBox" &&
                    (string)element.Attribute("AutomationProperties.AutomationId") ==
                    "OpenAiCodexReasoningEffort");
            var speed = subscriptionConfiguration
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "ComboBox" &&
                    (string)element.Attribute("AutomationProperties.AutomationId") ==
                    "OpenAiCodexSpeed");

            Assert.AreEqual("{Binding OpenAiAccessModes}", (string)authenticationMode.Attribute("ItemsSource"));
            Assert.AreEqual("{Binding SelectedOpenAiAccessMode}", (string)authenticationMode.Attribute("SelectedItem"));
            Assert.AreEqual("{Binding OpenAiSubscriptionModels}", (string)subscriptionModel.Attribute("ItemsSource"));
            Assert.AreEqual(
                "{Binding SelectedOpenAiSubscriptionModel}",
                (string)subscriptionModel.Attribute("SelectedItem"));
            var subscriptionModelTemplate = subscriptionModel
                .Descendants()
                .Single(element => element.Name.LocalName == "DataTemplate");
            var subscriptionModelText = subscriptionModelTemplate
                .Descendants()
                .Single(element => element.Name.LocalName == "TextBlock");
            Assert.AreEqual(
                "{Binding DisplayName}",
                (string)subscriptionModelText.Attribute("Text"));
            Assert.IsFalse(subscriptionModelTemplate
                .Descendants()
                .Any(element =>
                    (string)element.Attribute("Text") == "{Binding ModelId}"));
            Assert.AreEqual(
                "{Binding OpenAiSubscriptionReasoningEfforts}",
                (string)reasoningEffort.Attribute("ItemsSource"));
            Assert.AreEqual(
                "{Binding SelectedOpenAiSubscriptionReasoningEffort}",
                (string)reasoningEffort.Attribute("SelectedItem"));
            Assert.AreEqual(
                "{Binding OpenAiSubscriptionServiceTiers}",
                (string)speed.Attribute("ItemsSource"));
            Assert.AreEqual(
                "{Binding SelectedOpenAiSubscriptionServiceTier}",
                (string)speed.Attribute("SelectedItem"));
            Assert.IsTrue(subscriptionStatus
                .Descendants()
                .Any(element =>
                    element.Name.LocalName == "TextBlock" &&
                    (string)element.Attribute("Text") ==
                    "{Binding OpenAiSubscriptionStatus}"));
            Assert.IsTrue(apiConfiguration
                .Descendants()
                .Any(element =>
                    element.Name.LocalName == "DaisyInput" &&
                    (string)element.Attribute("Text") ==
                    "{Binding SelectedProviderApiKey.ApiKey}"));

            AssertSubscriptionCommand(
                subscriptionConfiguration,
                "OpenAiCodexConnect",
                "{Binding ConnectOpenAiSubscriptionCommand}",
                "{Binding ShowOpenAiSubscriptionConnectAction}");
            AssertSubscriptionCommand(
                subscriptionConfiguration,
                "OpenAiCodexSignIn",
                "{Binding LoginOpenAiSubscriptionCommand}",
                "{Binding ShowOpenAiSubscriptionSetupActions}");
            AssertSubscriptionCommand(
                subscriptionConfiguration,
                "OpenAiCodexLogout",
                "{Binding LogoutOpenAiSubscriptionCommand}",
                "{Binding ShowOpenAiSubscriptionLogoutAction}");
            var connect = subscriptionConfiguration
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "DaisyButton" &&
                    (string)element.Attribute("AutomationProperties.AutomationId") ==
                    "OpenAiCodexConnect");
            Assert.AreEqual("Primary", (string)connect.Attribute("Variant"));
        }

        [TestMethod]
        public void OpenAiProviderEditor_UsesSharedBrowserLauncherAndConfiguredAccessIndicator()
        {
            var document = XDocument.Load(FindSettingsViewPath());
            var configuredIcon = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "MaterialIcon" &&
                    (string)element.Attribute("Kind") == "CheckCircle" &&
                    (string)element.Attribute("IsVisible") ==
                    "{Binding HasConfiguredAccess}");
            var source = File.ReadAllText(FindSettingsViewPath() + ".cs");
            var viewModelSource = File.ReadAllText(FindSettingsViewModelPath());

            Assert.IsNotNull(configuredIcon);
            StringAssert.Contains(source, "_viewModel.OpenAiBrowserAction = OpenBrowserAsync;");
            StringAssert.Contains(source, "TopLevel.GetTopLevel(this)");
            StringAssert.Contains(source, "topLevel.Launcher.LaunchUriAsync(authorizationUri)");
            StringAssert.Contains(source, "_viewModel?.Dispose();");
            Assert.IsFalse(source.Contains("Process.Start", StringComparison.Ordinal));
            StringAssert.Contains(
                viewModelSource,
                "ShowOpenAiSubscriptionConfiguration && !IsOpenAiSubscriptionAuthenticated");
            StringAssert.Contains(
                viewModelSource,
                "ShowOpenAiSubscriptionConfiguration && IsOpenAiSubscriptionAuthenticated");
            StringAssert.Contains(
                viewModelSource,
                "!IsOpenAiSubscriptionAuthenticated &&");
        }

        [TestMethod]
        public void OpenAiSelections_PersistModeAndDeferCollectionSynchronization()
        {
            var viewModelSource = File.ReadAllText(FindSettingsViewModelPath());
            var modeChange = SourceSection(
                viewModelSource,
                "private async Task ChangeOpenAiAccessModeAsync",
                "private Task ConnectOpenAiSubscriptionAsync");
            var stateChanged = SourceSection(
                viewModelSource,
                "private void OnAiAccessManagerStateChanged",
                "private static string SanitizeAuthenticationError");

            StringAssert.Contains(
                modeChange,
                "aiSettings.OpenAiAccessMode = mode;");
            StringAssert.Contains(modeChange, "AppSettings.Save();");
            StringAssert.Contains(modeChange, "await Task.Yield();");
            StringAssert.Contains(
                stateChanged,
                "Dispatcher.UIThread.Post(");
            StringAssert.Contains(
                stateChanged,
                "() => SynchronizeOpenAiAccessState(preserveStatus));");
            Assert.IsFalse(
                stateChanged.Contains("Dispatcher.UIThread.CheckAccess()", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ProviderEditor_UsesResizableModelSectionsAndHoverRowDeleteActions()
        {
            var document = XDocument.Load(FindSettingsViewPath());
            var splitter = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "GridSplitter" &&
                    (string)element.Attribute("AutomationProperties.AutomationId") == "ProviderModelsSplitter");
            var modelLists = document
                .Descendants()
                .Where(element =>
                    element.Name.LocalName == "ListBox" &&
                    ((string)element.Attribute("ItemsSource") == "{Binding TextProviderModels}" ||
                     (string)element.Attribute("ItemsSource") == "{Binding ImageProviderModels}"))
                .ToList();

            Assert.AreEqual("Rows", (string)splitter.Attribute("ResizeDirection"));
            Assert.AreEqual("PreviousAndNext", (string)splitter.Attribute("ResizeBehavior"));
            Assert.AreEqual("16", (string)splitter.Attribute("KeyboardIncrement"));
            Assert.AreEqual("1", (string)splitter.Attribute("DragIncrement"));
            Assert.AreEqual("True", (string)splitter.Attribute("Focusable"));
            Assert.AreEqual(2, modelLists.Count);

            foreach (var modelList in modelLists)
            {
                Assert.AreEqual("provider-models", (string)modelList.Attribute("Classes"));
                var deleteButton = modelList
                    .Descendants()
                    .Single(element =>
                        element.Name.LocalName == "DaisyButton" &&
                        (string)element.Attribute("Classes") == "model-delete");

                Assert.AreEqual("{Binding}", (string)deleteButton.Attribute("Tag"));
                Assert.AreEqual("RemoveProviderModel_Click", (string)deleteButton.Attribute("Click"));
                Assert.AreEqual("Error", (string)deleteButton.Attribute("Variant"));
                Assert.AreEqual("{DynamicResource DaisyErrorBrush}", (string)deleteButton.Attribute("Background"));
                Assert.AreEqual("White", (string)deleteButton.Attribute("Foreground"));
                Assert.AreEqual("1", (string)deleteButton.Attribute("Grid.Column"));
                Assert.AreEqual("Auto,Auto", (string)deleteButton.Parent.Attribute("ColumnDefinitions"));
                Assert.AreEqual("Left", (string)deleteButton.Parent.Attribute("HorizontalAlignment"));
            }

            var styles = document
                .Descendants()
                .Where(element => element.Name.LocalName == "Style")
                .ToList();
            var baseDeleteStyle = styles.Single(element =>
                (string)element.Attribute("Selector") ==
                "ListBox.provider-models ListBoxItem daisy|DaisyButton.model-delete");
            var hoverDeleteStyle = styles.Single(element =>
                (string)element.Attribute("Selector") ==
                "ListBox.provider-models ListBoxItem:pointerover daisy|DaisyButton.model-delete");
            var focusDeleteStyle = styles.Single(element =>
                (string)element.Attribute("Selector") ==
                "ListBox.provider-models ListBoxItem:focus-within daisy|DaisyButton.model-delete");

            AssertStyleSetter(baseDeleteStyle, "Opacity", "0");
            AssertStyleSetter(baseDeleteStyle, "IsHitTestVisible", "False");
            AssertStyleSetter(baseDeleteStyle, "Focusable", "False");
            AssertStyleSetter(hoverDeleteStyle, "Opacity", "1");
            AssertStyleSetter(hoverDeleteStyle, "IsHitTestVisible", "True");
            AssertStyleSetter(hoverDeleteStyle, "Focusable", "True");
            AssertStyleSetter(focusDeleteStyle, "Opacity", "1");
            AssertStyleSetter(focusDeleteStyle, "IsHitTestVisible", "True");
            AssertStyleSetter(focusDeleteStyle, "Focusable", "True");

            var codeBehind = File.ReadAllText(FindSettingsViewPath() + ".cs");
            StringAssert.Contains(codeBehind, "private void RemoveProviderModel_Click");
            StringAssert.Contains(codeBehind, "viewModel.RemoveModelCommand.Execute(model);");
        }

        [TestMethod]
        public void ProviderModelSearchSetters_RefreshOnlyTheirOwnCollections()
        {
            var source = File.ReadAllText(FindSettingsViewModelPath());
            var textProperty = SourceSection(
                source,
                "public string TextModelSearchQuery",
                "private string _imageModelSearchQuery");
            var imageProperty = SourceSection(
                source,
                "public string ImageModelSearchQuery",
                "private string _newModelName");

            StringAssert.Contains(textProperty, "RefreshTextProviderModels();");
            Assert.IsFalse(textProperty.Contains("RefreshImageProviderModels();", StringComparison.Ordinal));
            StringAssert.Contains(imageProperty, "RefreshImageProviderModels();");
            Assert.IsFalse(imageProperty.Contains("RefreshTextProviderModels();", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ProviderModelSearch_MatchesModelIdsCaseInsensitively()
        {
            var matchesModelQuery = typeof(SettingsViewModel).GetMethod(
                "MatchesModelQuery",
                BindingFlags.Static | BindingFlags.NonPublic);
            var model = new AiModelOption("black-forest-labs/FLUX.2-pro", true);

            Assert.IsNotNull(matchesModelQuery);
            Assert.AreEqual(true, matchesModelQuery.Invoke(null, new object[] { model, "flux" }));
            Assert.AreEqual(false, matchesModelQuery.Invoke(null, new object[] { model, "gemini" }));
        }

        [TestMethod]
        public void ProviderDisplayText_UsesProviderName()
        {
            var provider = new ProviderApiKeyViewModel { ProviderType = "OpenRouter" };

            Assert.AreEqual("OpenRouter", provider.ToString());
        }

        [TestMethod]
        public void SettingsSidebarItem_IsNotRememberedForStartup()
        {
            var shouldRemember = typeof(MainWindow).GetMethod(
                "ShouldRememberSidebarItem",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(shouldRemember);
            Assert.AreEqual(false, shouldRemember.Invoke(null, new object[] { new ToolkitSettingsItem() }));
            Assert.AreEqual(true, shouldRemember.Invoke(null, new object[] { new ToolkitToolSidebarItem() }));
        }

        [TestMethod]
        public void SettingsWindow_CanOpenDirectlyOnAiConnections()
        {
            var source = File.ReadAllText(FindSettingsViewPath() + ".cs");

            StringAssert.Contains(source, "internal SettingsWindow(SettingsSection initialSection)");
            StringAssert.Contains(source, "GetSettingsNavigationList().SelectedIndex = (int)section;");
            StringAssert.Contains(source, "this.FindControl<ListBox>(\"SettingsNavigationList\")");

            var document = XDocument.Load(FindSettingsViewPath());
            var settingsNavigation = document
                .Descendants()
                .Single(element => element.Attributes().Any(attribute =>
                    attribute.Name.LocalName == "Name" &&
                    attribute.Value == "SettingsNavigationList"));
            var navigationItems = settingsNavigation
                .Elements()
                .Where(element => element.Name.LocalName == "ListBoxItem")
                .ToList();
            var connectionsItem = navigationItems
                .Single(element => element.Attributes().Any(attribute =>
                    attribute.Name.LocalName == "Name" &&
                    attribute.Value == "ConnectionsSettingsNavigationItem"));

            Assert.AreEqual(1, navigationItems.IndexOf(connectionsItem));
        }

        [TestMethod]
        public void SettingsWindow_UsesDaisyButtonsWithNonGhostVariants()
        {
            var document = XDocument.Load(FindSettingsViewPath());
            var standardButtons = document
                .Descendants()
                .Where(element => element.Name.LocalName == "Button")
                .ToList();
            var daisyButtons = document
                .Descendants()
                .Where(element => element.Name.LocalName == "DaisyButton")
                .ToList();
            var navigationButtons = daisyButtons
                .Where(element =>
                    (string)element.Attribute("Click") == "SettingsNavigationButton_Click")
                .ToList();

            Assert.AreEqual(0, standardButtons.Count);
            Assert.IsTrue(daisyButtons.Count > 0);
            Assert.IsTrue(daisyButtons.All(button =>
                button.Attribute("Variant") != null));
            Assert.IsFalse(daisyButtons.Any(button =>
                (string)button.Attribute("Variant") == "Ghost"));
            Assert.AreEqual(4, navigationButtons.Count);
            Assert.IsTrue(navigationButtons.All(button =>
                (string)button.Attribute("Variant") == "Default"));

            var source = File.ReadAllText(FindSettingsViewPath() + ".cs");
            Assert.IsFalse(source.Contains("new Button", StringComparison.Ordinal));
            StringAssert.Contains(source, "new DaisyButton");
            StringAssert.Contains(source, "DaisyButtonVariant.Primary");
            StringAssert.Contains(source, "DaisyButtonVariant.Warning");
            StringAssert.Contains(source, "DaisyButtonVariant.Default");
        }

        [TestMethod]
        public void AppTooltips_UseContrastingThemeColors()
        {
            var document = XDocument.Load(FindAppViewPath());
            var styles = document
                .Descendants()
                .Where(element => element.Name.LocalName == "Style")
                .ToList();
            var toolTipStyle = styles.Single(element =>
                (string)element.Attribute("Selector") == "ToolTip");
            var toolTipTextStyle = styles.Single(element =>
                (string)element.Attribute("Selector") == "ToolTip TextBlock");

            AssertStyleSetter(toolTipStyle, "Background", "{DynamicResource DaisyNeutralBrush}");
            AssertStyleSetter(toolTipStyle, "Foreground", "{DynamicResource DaisyNeutralContentBrush}");
            AssertStyleSetter(toolTipTextStyle, "Foreground", "{DynamicResource DaisyNeutralContentBrush}");
        }

        private static void SetField(SettingsViewModel viewModel, string name, object value)
        {
            var field = typeof(SettingsViewModel).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(viewModel, value);
        }

        private static string FindSettingsViewPath()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, "OpenSourceToolkit.NET", "Views", "SettingsWindow.axaml");
                if (File.Exists(candidate))
                    return candidate;

                directory = directory.Parent;
            }

            Assert.Fail("Could not locate SettingsWindow.axaml from the test output directory.");
            return null;
        }

        private static string FindAppViewPath()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, "OpenSourceToolkit.NET", "App.axaml");
                if (File.Exists(candidate))
                    return candidate;

                directory = directory.Parent;
            }

            Assert.Fail("Could not locate App.axaml from the test output directory.");
            return null;
        }

        private static string FindSettingsViewModelPath()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, "OpenSourceToolkit.NET", "ViewModels", "SettingsViewModel.cs");
                if (File.Exists(candidate))
                    return candidate;

                directory = directory.Parent;
            }

            Assert.Fail("Could not locate SettingsViewModel.cs from the test output directory.");
            return null;
        }

        private static string SourceSection(string source, string startMarker, string endMarker)
        {
            var startIndex = source.IndexOf(startMarker, StringComparison.Ordinal);
            Assert.IsTrue(startIndex >= 0, $"Could not find source marker: {startMarker}");
            var endIndex = source.IndexOf(endMarker, startIndex, StringComparison.Ordinal);
            Assert.IsTrue(endIndex > startIndex, $"Could not find source marker after start: {endMarker}");
            return source.Substring(startIndex, endIndex - startIndex);
        }

        private static void AssertStyleSetter(XElement style, string property, string expectedValue)
        {
            var setter = style
                .Elements()
                .Single(element =>
                    element.Name.LocalName == "Setter" &&
                    (string)element.Attribute("Property") == property);

            Assert.AreEqual(expectedValue, (string)setter.Attribute("Value"));
        }

        private static void AssertSubscriptionCommand(
            XElement subscriptionConfiguration,
            string automationId,
            string commandBinding,
            string visibilityBinding)
        {
            var button = subscriptionConfiguration
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "DaisyButton" &&
                    (string)element.Attribute("AutomationProperties.AutomationId") ==
                    automationId);

            Assert.AreEqual(commandBinding, (string)button.Attribute("Command"));
            Assert.AreEqual(visibilityBinding, (string)button.Attribute("IsVisible"));
        }

    }
}
