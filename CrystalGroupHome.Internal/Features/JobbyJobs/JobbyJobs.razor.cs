using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Blazorise;
using Blazorise.Bootstrap5;
using Blazorise.DataGrid;
using CrystalGroupHome.SharedRCL.Data;
using CrystalGroupHome.Internal.Features.JobbyJobs.Data;
using CrystalGroupHome.Internal.Common.Data.Jobs;

namespace CrystalGroupHome.Internal.Features.JobbyJobs
{
    public class JobbyJobsBase : ComponentBase
    {
        [Inject] public IJobService JobService { get; set; } = default!;

        protected const int itemsPerPage = 10;

        protected JobHeadDTO_Base? selectedJobHead;
        protected PaginatedResult<JobHeadDTO_Base> jobHeads = new();

        protected JobDTO_Extended? selectedJobHeadExtended;
        protected PaginatedResult<JobDTO_Extended> jobHeadsExtended = new();

        protected async Task LoadJobHeads(DataGridReadDataEventArgs<JobHeadDTO_Base> args)
        {
            var pageNumber = args.Page;
            var pageSize = args.PageSize;

            jobHeads = await JobService.GetJobHeadsPaginatedAsync<JobHeadDTO_Base>(pageNumber, pageSize);
        }

        protected async Task LoadJobHeadsExtended(DataGridReadDataEventArgs<JobDTO_Extended> args)
        {
            var pageNumber = args.Page;
            var pageSize = args.PageSize;

            jobHeadsExtended = await JobService.GetJobHeadsPaginatedAsync<JobDTO_Extended>(pageNumber, pageSize);
        }
    }
}
