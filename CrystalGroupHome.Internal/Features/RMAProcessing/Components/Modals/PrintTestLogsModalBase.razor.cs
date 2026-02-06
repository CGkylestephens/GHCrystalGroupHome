using Blazorise;
using CrystalGroupHome.Internal.Features.RMAProcessing.Data;
using CrystalGroupHome.Internal.Features.RMAProcessing.Models;
using CrystalGroupHome.SharedRCL.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace CrystalGroupHome.Internal.Features.RMAProcessing.Components.Modals
{
    public partial class PrintTestLogsModalBase : ComponentBase
    {
        [Inject] protected IRMAFileService RMAFileService { get; set; } = default!;
        [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] protected ILogger<PrintTestLogsModalBase>? Logger { get; set; }

        [Parameter] public bool IsVisible { get; set; }
        [Parameter] public EventCallback<bool> IsVisibleChanged { get; set; }
        [Parameter] public string? RmaNumber { get; set; }
        [Parameter] public string? RmaLineNumber { get; set; }
        [Parameter] public string? SerialNumber { get; set; }
        [Parameter] public List<RMAFileAttachmentDTO> AvailableTestLogFiles { get; set; } = new();

        protected HashSet<int> SelectedTestLogFileIds { get; set; } = new();
        protected PrintTestLogsOptions PrintOptions { get; set; } = new();
        protected List<int> SelectedTestLogFiles => SelectedTestLogFileIds.ToList();

        private readonly string[] TestLogCategoryShortNames = { "TestBurnInLogs", "TestLogs" };

        protected override void OnParametersSet()
        {
            // Reset modal state when it becomes visible
            if (IsVisible && !SelectedTestLogFileIds.Any())
            {
                SelectedTestLogFileIds.Clear();
                PrintOptions = new();
            }
        }

        // Handle ALL modal closing scenarios by notifying parent
        protected async Task OnModalClosing(ModalClosingEventArgs e)
        {
            // Clear internal state
            SelectedTestLogFileIds.Clear();
            PrintOptions = new();
            
            // CRITICAL: Tell the parent to hide the modal
            await IsVisibleChanged.InvokeAsync(false);
        }

        // Handle explicit close button clicks
        protected async Task CloseModal()
        {
            // Clear internal state
            SelectedTestLogFileIds.Clear();
            PrintOptions = new();
            
            // Tell the parent to hide the modal
            await IsVisibleChanged.InvokeAsync(false);
        }

        protected void SelectAllTestLogs()
        {
            SelectedTestLogFileIds = AvailableTestLogFiles.Select(f => f.Id).ToHashSet();
            StateHasChanged();
        }

        protected void DeselectAllTestLogs()
        {
            SelectedTestLogFileIds.Clear();
            StateHasChanged();
        }

        protected bool IsTestLogFileSelected(int id) => SelectedTestLogFileIds.Contains(id);

        protected void OnTestLogFileSelectionChanged(int fileId, bool isSelected)
        {
            if (isSelected) 
                SelectedTestLogFileIds.Add(fileId);
            else 
                SelectedTestLogFileIds.Remove(fileId);
            
            StateHasChanged();
        }

        protected async Task PrintSelectedTestLogs()
        {
            if (!SelectedTestLogFileIds.Any()) return;

            try
            {
                var request = new PrintTestLogsRequest
                {
                    RmaNumber = RmaNumber ?? string.Empty,
                    RmaLineNumber = RmaLineNumber,
                    SerialNumber = SerialNumber,
                    SelectedFileIds = SelectedTestLogFileIds.ToList(),
                    PrintOptions = PrintOptions
                };

                var result = await RMAFileService.PrintTestLogsAsync(request);
                if (result.Success && result.PdfData != null)
                {
                    var base64Data = Convert.ToBase64String(result.PdfData);
                    var opened = await JSRuntime.InvokeAsync<bool>("openPdfInline", base64Data);
                    if (opened) 
                        await CloseModal();
                }
                else
                {
                    await JSRuntime.InvokeVoidAsync("alert", $"Error creating print job: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "PrintSelectedTestLogs error");
                await JSRuntime.InvokeVoidAsync("alert", $"Error: {ex.Message}");
            }
        }

        protected string FormatFileSize(long bytes) => FileHelpers.FormatFileSize(bytes);

        protected bool IsTestLogFile(RMAFileAttachmentDTO file) =>
            TestLogCategoryShortNames.Contains(file.Category?.ShortName, StringComparer.OrdinalIgnoreCase);
    }
}