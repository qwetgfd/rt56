import { HttpRequest } from '@angular/common/http';
import { GUEST_GROUP_ID } from './guest.util';

export function guestOk<T>(result: T) {
  return {
    responseCode: 200,
    responseMessage: ['Success'],
    result,
  };
}

const GUEST_SECURITY_GROUP_ID_STAGING = '00000000-0000-0000-0000-000000000002';

/** Mirrors dbo.di_SecurityGroup seed in local_process_config_seed.sql */
function guestSecurityGroupsFromDb(loginId: string) {
  const preselected = loginId === 'guest';
  return [
    {
      securityGroupId: GUEST_GROUP_ID,
      securityGroupName: 'Product Development',
      userSelectedGroup: preselected,
    },
    {
      securityGroupId: GUEST_SECURITY_GROUP_ID_STAGING,
      securityGroupName: 'Product Development Staging',
      userSelectedGroup: false,
    },
  ];
}

// #region Sharepoint Workspace - AY
const GUEST_PROCESS_TYPES = [
  { processTypeId: 1, processTypeName: 'Online' },
  { processTypeId: 2, processTypeName: 'Offline - Shared Location' },
  { processTypeId: 3, processTypeName: 'Offline - Blob Storage' },
  { processTypeId: 6, processTypeName: 'SharePoint Workspace' },
];

const GUEST_SCHEDULER_TYPES = [
  { schedulerTypeId: 1, schedulerType: 'One Time' },
  { schedulerTypeId: 2, schedulerType: 'Daily' },
  { schedulerTypeId: 3, schedulerType: 'Weekly' },
  { schedulerTypeId: 4, schedulerType: 'Custom' },
];

const GUEST_WEEK_DAYS = [
  { id: 1, weekDayName: 'Sunday' },
  { id: 2, weekDayName: 'Monday' },
  { id: 3, weekDayName: 'Tuesday' },
  { id: 4, weekDayName: 'Wednesday' },
  { id: 5, weekDayName: 'Thursday' },
  { id: 6, weekDayName: 'Friday' },
  { id: 7, weekDayName: 'Saturday' },
];

const GUEST_FREQUENCY_HOURS = [
  { id: 1, frequencyHour: '1' },
  { id: 2, frequencyHour: '2' },
  { id: 3, frequencyHour: '4' },
  { id: 4, frequencyHour: '6' },
  { id: 5, frequencyHour: '8' },
  { id: 6, frequencyHour: '12' },
  { id: 7, frequencyHour: '24' },
];

const GUEST_DS_CONFIG = {
  consumerApplicationId: '00000000-0000-0000-0000-000000000000',
  sourceDataObjId: '00000000-0000-0000-0000-000000000000',
};

const currentMonth = new Date().getMonth() + 1;

const GUEST_DS_REGIONS = [
  { region_ident: 1, region: 'APAC' },
  { region_ident: 2, region: 'Europe' },
  { region_ident: 3, region: 'North America' },
  { region_ident: 4, region: 'LATAM' },
  { region_ident: 5, region: 'MEA' },
];

const GUEST_DS_SUBREGIONS = [
  { subsubregion_code: 'SEA', subsubregion: 'Southeast Asia', region_ident: 1 },
  { subsubregion_code: 'AUS', subsubregion: 'Australia', region_ident: 1 },
  { subsubregion_code: 'WEU', subsubregion: 'Western Europe', region_ident: 2 },
  { subsubregion_code: 'EEU', subsubregion: 'Eastern Europe', region_ident: 2 },
  { subsubregion_code: 'USA', subsubregion: 'USA', region_ident: 3 },
  { subsubregion_code: 'CAN', subsubregion: 'Canada', region_ident: 3 },
  { subsubregion_code: 'BRA', subsubregion: 'Brazil', region_ident: 4 },
  { subsubregion_code: 'UAE', subsubregion: 'UAE', region_ident: 5 },
];

const GUEST_DS_CLIENTS = [
  { client_ident: 1, client_full_name: 'ABC Corporation APAC', region_ident: 1, subsubregion_code: 'SEA' },
  { client_ident: 2, client_full_name: 'EuroBank Financial', region_ident: 2, subsubregion_code: 'WEU' },
  { client_ident: 3, client_full_name: 'NorthStar Retail Group', region_ident: 3, subsubregion_code: 'USA' },
  { client_ident: 4, client_full_name: 'LatAm Telecom', region_ident: 4, subsubregion_code: 'BRA' },
  { client_ident: 5, client_full_name: 'Mena Oil & Gas', region_ident: 5, subsubregion_code: 'UAE' },
  { client_ident: 6, client_full_name: 'Pacific Insurance', region_ident: 1, subsubregion_code: 'AUS' },
];

const GUEST_GRAPH_GROUPS = [
  {
    id: GUEST_GROUP_ID,
    displayName: 'Product Development',
    groupTypes: [],
    securityEnabled: true,
  },
  {
    id: '00000000-0000-0000-0000-000000000002',
    displayName: 'Product Development Staging',
    groupTypes: [],
    securityEnabled: true,
  },
];

function graphGroupSearchTerm(url: string): string {
  const match = url.match(/startswith\(displayname,'([^']*)'\)/i);
  return match ? decodeURIComponent(match[1]).toLowerCase() : '';
}

function filterGraphGroups(url: string) {
  const term = graphGroupSearchTerm(url);
  return GUEST_GRAPH_GROUPS.filter(
    (group) => !term || group.displayName.toLowerCase().startsWith(term)
  );
}

function filterSubRegions(regionId: string | null) {
  if (!regionId) {
    return GUEST_DS_SUBREGIONS;
  }
  const id = Number(regionId);
  return GUEST_DS_SUBREGIONS.filter((item) => item.region_ident === id);
}

function filterClients(regionId: string | null, subregionCode: string | null) {
  return GUEST_DS_CLIENTS.filter((item) => {
    const regionMatch = !regionId || item.region_ident === Number(regionId);
    const subRegionMatch = !subregionCode || item.subsubregion_code === subregionCode;
    return regionMatch && subRegionMatch;
  });
}

export function resolveGuestMock(req: HttpRequest<unknown>): unknown | 'GRAPH_PHOTO' | null {
  const url = req.url.toLowerCase();
  const method = req.method.toUpperCase();

  if (url.includes('graph.microsoft.com')) {
    if (url.includes('photo')) {
      return 'GRAPH_PHOTO';
    }
    if (url.includes('/groups')) {
      return {
        '@odata.context': 'https://graph.microsoft.com/v1.0/$metadata#groups',
        value: filterGraphGroups(url),
      };
    }
    if (url.includes('memberof')) {
      return {
        '@odata.context': 'https://graph.microsoft.com/v1.0/$metadata#directoryObjects',
        value: [
          {
            id: GUEST_GROUP_ID,
            displayName: 'Product Development',
            groupTypes: [],
            securityEnabled: true,
          },
        ],
      };
    }
    return {
      displayName: 'Guest User',
      mail: 'guest@local.dev',
      jobTitle: 'Guest',
      userPrincipalName: 'guest@local.dev',
      employeeId: 'guest',
    };
  }

  if (url.includes('securitygroups')) {
    const loginMatch = url.match(/[?&]loginid=([^&]+)/i);
    const loginId = loginMatch ? decodeURIComponent(loginMatch[1]) : '';
    return guestOk(guestSecurityGroupsFromDb(loginId));
  }

  // #region Sharepoint Workspace - AY
  if (url.includes('getprocesstype')) {
    return guestOk(GUEST_PROCESS_TYPES);
  }
  // #endregion

  if (url.includes('getfileserverdetails')) {
    return guestOk([{ fileServerId: 1, serverName: 'LOCAL-DEV-FS' }]);
  }

  if (url.includes('getschedulertype')) {
    return guestOk(GUEST_SCHEDULER_TYPES);
  }

  if (url.includes('getweekdayname')) {
    return guestOk(GUEST_WEEK_DAYS);
  }

  if (url.includes('getfrequencyhour')) {
    return guestOk(GUEST_FREQUENCY_HOURS);
  }

  if (url.includes('getdsconfiguration')) {
    return guestOk(GUEST_DS_CONFIG);
  }

  if (url.includes('getregionsbysecuritygroup')) {
    return guestOk(GUEST_DS_REGIONS);
  }

  if (url.includes('getsubregions')) {
    return guestOk(filterSubRegions(req.headers.get('regionId')));
  }

  if (url.includes('getclients')) {
    return guestOk(
      filterClients(req.headers.get('regionId'), req.headers.get('subregionCode'))
    );
  }

  if (url.includes('getdatabasenames')) {
    return guestOk([
      {
        id: 1,
        databaseName: 'DI_Staging',
        databaseServer: 'staging-server.database.windows.net',
        defaultDB: true,
        groupBy: 'Default',
      },
      {
        id: 2,
        databaseName: 'DI_Master',
        databaseServer: 'master-server.database.windows.net',
        defaultDB: false,
        groupBy: 'Exclusive',
      },
    ]);
  }

  if (url.includes('getstorageaccountdetails')) {
    return guestOk([
      {
        storageAccountId: 1,
        storageAccountName: 'local-dev-storage',
        containerName: 'local-container',
        configurationProcessType: 1,
      },
    ]);
  }

  if (url.includes('getdirulesetnamesbysecgrpid')) {
    return guestOk([
      {
        ruleSetNameId: 'guest-ruleset-1',
        ruleSetName: 'Customer Data Validation',
        creationDateTime: new Date().toISOString(),
        updationDateTime: new Date().toISOString(),
      },
    ]);
  }

  if (url.includes('getprocesslist')) {
    return guestOk({
      processRowCount: 22,
      newProcessCount: 3,
      currentMonth,
      currentMonthName: '',
    });
  }

  if (url.includes('getclientlist')) {
    return guestOk({
      totalClients: 8,
      activeClients: 5,
      currentMonth,
      currentMonthName: '',
    });
  }

  if (url.includes('getfilelist')) {
    return guestOk({
      totalUploadedFiles: 23,
      successCount: 17,
      failureCount: 6,
      newFileProcessCount: 4,
      currentMonth,
      currentMonthName: '',
    });
  }

  if (url.includes('countfileuploadsbyprocesstype')) {
    return guestOk([
      { totalUploadedFiles: 23, processTypeId: 1, processType: 'Online' },
    ]);
  }

  if (url.includes('dashboardrealtimeprocessingstatuslist')) {
    return guestOk([
      {
        configurationId: 'guest-config-1',
        processName: 'DI_ALALLAD24',
        uploadFileId: 'guest-upload-1',
        fileName: 'DI_MultiSheet.xlsx',
        creationDateTime: new Date().toISOString(),
        processStatusId: 3,
        processStatusName: 'Processed',
      },
      {
        configurationId: 'guest-config-2',
        processName: 'DI_ALALLAD23',
        uploadFileId: 'guest-upload-2',
        fileName: 'DI_Sales_Data.csv',
        creationDateTime: new Date(Date.now() - 86400000).toISOString(),
        processStatusId: 3,
        processStatusName: 'Processed',
      },
      {
        configurationId: 'guest-config-3',
        processName: 'APAC_Sales_Daily_Ingestion',
        uploadFileId: 'guest-upload-3',
        fileName: 'APAC_Daily.xlsx',
        creationDateTime: new Date(Date.now() - 172800000).toISOString(),
        processStatusId: 2,
        processStatusName: 'Processing',
      },
    ]);
  }

  if (url.includes('diframeworkutilization')) {
    return guestOk([
      { totalFileCount: 10, month: currentMonth, monthName: '', clientId: 1, clientName: 'AllianceOne' },
      { totalFileCount: 8, month: currentMonth, monthName: '', clientId: 2, clientName: 'APAC' },
      { totalFileCount: 6, month: currentMonth, monthName: '', clientId: 3, clientName: 'CANADA' },
      { totalFileCount: 5, month: currentMonth, monthName: '', clientId: 4, clientName: 'India' },
      { totalFileCount: 4, month: currentMonth, monthName: '', clientId: 5, clientName: 'MLH' },
    ]);
  }

  if (url.includes('utilizationbyregions')) {
    return guestOk([
      { regionId: 1, regionName: 'AllianceOne', totalFileCount: 10 },
      { regionId: 2, regionName: 'APAC', totalFileCount: 8 },
      { regionId: 3, regionName: 'CANADA', totalFileCount: 6 },
      { regionId: 4, regionName: 'India', totalFileCount: 5 },
      { regionId: 5, regionName: 'MLH', totalFileCount: 4 },
    ]);
  }

  if (
    url.includes('getallprocessnamesbyloginid') ||
    url.includes('getallprocessnamesbyloginidbyterm')
  ) {
    return guestOk([
      {
        flpConfigurationId: 'guest-process-1',
        processNames: 'APAC_Sales_Daily_Ingestion',
        description: 'Daily APAC sales ingestion',
      },
      {
        flpConfigurationId: 'guest-process-2',
        processNames: 'EUR_Finance_Monthly_Close',
        description: 'Monthly finance close process',
      },
      {
        flpConfigurationId: 'guest-process-3',
        processNames: 'DI_ALALLAD24',
        description: 'Sample ingestion process',
      },
    ]);
  }

  if (url.includes('getlandinglayeruploadconfiguration')) {
    return guestOk({
      noOfAllowedFilesToUpload: 10,
      totalFileSize: 300,
    });
  }

  if (url.includes('getalldatatypenames')) {
    return guestOk([
      { datatypeId: 1, datatypeName: 'varchar' },
      { datatypeId: 2, datatypeName: 'int' },
      { datatypeId: 3, datatypeName: 'datetime' },
    ]);
  }

  if (url.includes('getalldiregions')) {
    return guestOk([
      { id: 1, name: 'APAC' },
      { id: 2, name: 'Europe' },
      { id: 3, name: 'North America' },
    ]);
  }

  if (url.includes('getalldisubregions')) {
    return guestOk([
      { id: 1, name: 'Southeast Asia' },
      { id: 2, name: 'Australia' },
      { id: 3, name: 'Western Europe' },
    ]);
  }

  if (url.includes('getalldiclientnames')) {
    return guestOk([
      { id: 1, name: 'ABC Corporation APAC' },
      { id: 2, name: 'EuroBank Financial' },
      { id: 3, name: 'NorthStar Retail Group' },
    ]);
  }

  if (url.includes('getenglishonlycharacters') || url.includes('englishonly')) {
    return guestOk([]);
  }

  if (url.includes('getdatetimeformats') || url.includes('datatimeformat')) {
    return guestOk([
      { formatId: 1, format: 'yyyy-MM-dd', example: '2026-01-02' },
      { formatId: 2, format: 'MM/dd/yyyy', example: '01/02/2026' },
    ]);
  }

  if (url.includes('fileuploadstatus')) {
    return guestOk([
      {
        clientId: 1,
        clientName: 'ABC Corporation APAC',
        fileConfigurationStatusList: [
          {
            flpConfigurationID: 'guest-process-1',
            flpConfigurationName: 'APAC_Sales_Daily_Ingestion',
            uploadedFiles: [
              {
                uploadFileId: 'guest-upload-1',
                uploadFileName: 'APAC_Daily.xlsx',
                fileCreationDate: new Date(),
                fileProcessStatusId: 3,
                fileProcessstatusName: 'Processed',
                databaseName: 'DI_Staging',
                tableName: 'SalesDaily',
                totalRecords: 1000,
                processedRecords: 1000,
                duplicateRecords: 0,
                completionTime: new Date().toISOString(),
                durationInSeconds: 45,
                tabName: 'Sheet1',
                fileProcessingServerTypeId: 1,
              },
            ],
          },
        ],
      },
    ]);
  }

  if (url.includes('getprocessedfilelist')) {
    return guestOk([]);
  }

  if (url.includes('getfileprocessconfigurationlist')) {
    return guestOk([]);
  }

  if (url.includes('getcampaignuseraccessinfo')) {
    return guestOk([]);
  }

  if (url.includes('getglobalrulecreationaccess')) {
    return guestOk(true);
  }

  if (url.includes('getuserdetail')) {
    return guestOk({
      userDetail: {
        isAdmin: true,
      },
    });
  }

  if (url.includes('savesecuritygroup') || url.includes('updatelogin')) {
    return guestOk(true);
  }

  if (url.includes('fillcache')) {
    return guestOk(true);
  }

  // #region Sharepoint Workspace - AY
  // Prod returns '' when unique; that clears auto-generated name in getProcessName(). Return requested name instead.
  if (url.includes('processnameexists')) {
    const match = url.match(/[?&]processname=([^&]+)/i);
    const requestedName = match ? decodeURIComponent(match[1]) : '';
    return guestOk(requestedName);
  }

  if (url.includes('getprefixes')) {
    return guestOk([{ id: 1, prefixName: 'DI_' }]);
  }

  if (method === 'GET') {
    return guestOk([]);
  }

  if (method === 'POST' || method === 'PUT' || method === 'PATCH') {
    return guestOk(true);
  }

  return guestOk(null);
}