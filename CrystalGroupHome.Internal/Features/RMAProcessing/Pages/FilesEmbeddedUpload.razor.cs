using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using CrystalGroupHome.Internal.Features.RMAProcessing.Data;
using CrystalGroupHome.Internal.Features.RMAProcessing.Models;
using Microsoft.Extensions.Logging;

namespace CrystalGroupHome.Internal.Features.RMAProcessing.Pages
{
    public class FilesEmbeddedUploadPageBase : BaseRMAFileUploadPage
    {
        [Inject] protected new ILogger<FilesEmbeddedUploadPageBase>? Logger { get; set; }

        protected override string ComponentName => "FilesEmbeddedUpload";
        protected override string ListUrlPath => "/rma-processing/files-embedded/list";

    }
}