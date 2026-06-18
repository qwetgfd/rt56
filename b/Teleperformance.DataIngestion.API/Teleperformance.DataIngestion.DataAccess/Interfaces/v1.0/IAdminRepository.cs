using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0
{
    public interface IAdminRepository
    {
        Task<string> GetContainerName();

        Task<int> InsertFile(FileValueRequest fileValueAPIRequest);

        Task<int> InsertEIBFile(EIBFileValueRequest request);
    }
}
