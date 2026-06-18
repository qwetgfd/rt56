using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Models.v1._0;
using SMBLibrary.Client;
using Teleperformance.DataIngestion.Models.Enums.v1._0;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0
{
    public interface ISMBLibraryServices
    {
        SMBResponse SMBRequest(CheckConnectivitySMBLibraryModel model, string flpConfigurationId, SMBRequestEnum sMBRequestEnum);
        bool CopyFileFromOneToAnotherLocation(CheckConnectivitySMBLibraryModel sourceModel, CheckConnectivitySMBLibraryModel destinationModel, string sourceFilePath, string destinationFilePath, string flpConfigurationId);

        (SMB2Client, ISMBFileStore) SMBRequest(CheckConnectivitySMBLibraryModel model, string flpConfigurationId);
        byte[] ReadFileFromServer(ISMBFileStore fileStore, string filePath);
        Stream GetFileStream(ISMBFileStore fileStore, string filePath, string flpConfigurationId);
        Task<bool> FileExistsInRemoteLocation(CheckConnectivitySMBLibraryModel model, string flpConfigurationId);

        //SMBResponse SMBRequest(CheckConnectivitySMBLibraryModel model, string flpConfigurationId, SMBRequestEnum sMBRequestEnum);
    }
}
