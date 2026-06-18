
export interface DatabaseSettings extends LandingLayerDestinationSettings {
  tableName: string;
  drop_history_table: boolean;
  drop_main_table: boolean;
  databaseConfigurationId: string;
  validate_fileschema: boolean;
  databaseName: string; // selected
  mergeData: boolean;
  createHistoryTable: boolean;
  deltaStorageAccountId: string;
  deltaContainerName: string;
  deltaJobId: string; 
  deltaSource: string;
  deltaTableName: string;
  deltaServerNameId: number;
  datalakeStorageAccountPath:string;
    
}

export interface LandingLayerDestinationSettings{
  landingLayerAcceptedPath : string;
  landingLayerRejectedPath : string;
}

export interface RequestDatabaseSetting extends DatabaseSettings {
  database_name:string
  table_name:string
}

