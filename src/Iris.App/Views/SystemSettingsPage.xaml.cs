using Iris.App.Services;
using Iris.App.ViewModels;
using Iris.App.Views.Dialogs;

namespace Iris.App.Views;

public partial class SystemSettingsPage : ContentPage
{
	private readonly SystemSettingsViewModel _vm;
	private readonly IDialogService _dialogs;

	public SystemSettingsPage(SystemSettingsViewModel vm, IDialogService dialogs)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		_dialogs = dialogs;

		// Same wait-then-react shape as DeploymentsPage's SelectApplicationRequested for all
		// four dialogs below: only check WasSaved once ShowAsync's Task has actually completed,
		// i.e. the dialog's window is fully closed — never react from inside the dialog's own
		// Save/Cancel handling.
		_vm.ConfigureRequested += async (_, dialogVm) =>
		{
			switch (dialogVm)
			{
				case ConfigureOpenBaoDialogViewModel openBao:
					await _dialogs.ShowAsync(new ConfigureOpenBaoDialog(openBao), "dlg.configure-openbao", 480, 460);
					if (openBao.WasSaved)
					{
						await _vm.LoadCommand.ExecuteAsync(null);
					}
					break;

				case ConfigureAwxDialogViewModel awx:
					await _dialogs.ShowAsync(new ConfigureAwxDialog(awx), "dlg.configure-awx", 480, 420);
					if (awx.WasSaved)
					{
						await _vm.LoadCommand.ExecuteAsync(null);
					}
					break;

				case ConfigureAnsibleDialogViewModel ansible:
					await _dialogs.ShowAsync(new ConfigureAnsibleDialog(ansible), "dlg.configure-ansible", 480, 420);
					if (ansible.WasSaved)
					{
						await _vm.LoadCommand.ExecuteAsync(null);
					}
					break;

				case ConfigureAzureDevOpsDialogViewModel azureDevOps:
					await _dialogs.ShowAsync(new ConfigureAzureDevOpsDialog(azureDevOps), "dlg.configure-azure-devops", 480, 380);
					if (azureDevOps.WasSaved)
					{
						await _vm.LoadCommand.ExecuteAsync(null);
					}
					break;

				case ConfigureNexusDialogViewModel nexus:
					await _dialogs.ShowAsync(new ConfigureNexusDialog(nexus), "dlg.configure-nexus", 480, 400);
					if (nexus.WasSaved)
					{
						await _vm.LoadCommand.ExecuteAsync(null);
					}
					break;
			}
		};

		_vm.ConfigureMailRequested += async (_, dialogVm) =>
		{
			await _dialogs.ShowAsync(new ConfigureMailDialog(dialogVm), "dlg.configure-mail", 560, 680);
			if (dialogVm.WasSaved)
			{
				await _vm.LoadCommand.ExecuteAsync(null);
			}
		};

		_vm.UnlockFallbackSecretsRequested += async (_, dialogVm) =>
		{
			await _dialogs.ShowAsync(new UnlockFallbackSecretsDialog(dialogVm), "dlg.unlock-fallback-secrets", 460, 340);
			if (dialogVm.WasSaved)
			{
				await _vm.LoadCommand.ExecuteAsync(null);
			}
		};
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		if (_vm.LoadCommand.CanExecute(null))
		{
			_vm.LoadCommand.Execute(null);
		}
	}
}
