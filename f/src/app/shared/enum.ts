
export const enum FileType {
    CommaSeparatedValues = 'csv',
    TextFiles = 'txt',
    MSExcel1 = 'xls',
    MSExcel2 = 'xlsx',
    MSExcel3 = 'xlsb'
}
export const enum ModalMessages {
    InvalidDataType = 'Invalid Data Type: Choose a different data type or correct your file.',
    CantProceedPleaseCorrectOtherDetails = `Can't proceed. Please correct other details.`,
    SomethingWentWrong = 'Something went wrong. Please contact the web admin.',
    ClientSettingsInvalid = 'Please complete Process Settings.',
    ConfigSourceDoesNotMatch = 'Selected configuration does not match your source.',
    InvalidDataSource = 'Correct data source to proceed.',
    InvalidDataSourceNewColumn = 'Correct data source to proceed. Some columns not found on source',
    ExcelWorksheetMoreThanOne = 'Excel file has more than one sheet. Can\'t process at the moment.',
    UniqueColumnName = 'Please choose another column name.',
    InvalidFile = 'Invalid File.',
    AdditionalSettingsNotProvided = 'Additional settings not provided.',
    SkipHeaderRowsMoreThanTotalRowcount = 'Value entered is higher than the file total row count',
    NoRowsToIngest = 'There are no rows in file to ingest.',
    NoSelectedFiles = 'There are no files to process',
    SecurityGroupDoesNotExist = 'Security Group does not exist.',
    HeaderSettingsHaveBeenChanged = 'Validations',
    HeaderSettingsHaveBeenChangedConfirm = 'Validation Rules may reset. Do you want to proceed?',
    EIBCreationTitle = 'EIB Creation',
    EIBCreationMessage = 'Any unsaved EIB Configuration will not be saved. Do you want to proceed?',
    NoContainerName = 'Selected storage account does not have container associated. Please select another storage account or contact administrator.'
}

export const enum ToastrMessages {
    SomethingWentWrong = 'Something went wrong. Please contact the web admin.',
    InvalidDataType = 'Invalid Data Type: Choose a different data type or correct your file.',
    InvalidFileExtension = 'Invalid file extension',
    FileIsTooLarge = 'File is too big.',
    InvalidDataSource = 'Correct data source to proceed',
    ProcessHasBeenModifed = 'Process has been modified successfully!',
    RuleSetCreatedSuccessfully = 'Rule Set created successfully',
    RuleSetUpdatedSuccessfully = 'Rule Set updated successfully',
    ColumnNamesNotProvided = `Can't proceed. File Column Names is required.`,
    UnableToSave = `Invalid Data: Something's wrong with your rule set.`,
    NoRulesToSave = `Unable to proceed, no rules to save.`,
    ValidatonRulesHaveBeenReset = 'Validation Rules have been reset',
    EIBColumnCountNotEqual = 'Unable to map. Column count does not match'
}

export const enum ModalTitles {
    TPDataIngestion = '',
    CorrectionNeededOnSource = 'Corrections Needed on Source!'

}

export enum DataSourceType {
    Default = 1,
    DataBricks = 2,
    LandingLayer = 3
}

export enum RuleTypeNames {
    Required = 1,
    Unique = 2,
    Custom = 3,
    Format = 4,
    Value = 5,
    BEValidation = 6,
    GenericRules = 7
}

export enum SubRuleTypes {
    Pattern = 1,
    Length = 2,
    ComparisonOperators = 3,
    NumericRange = 4,
    All = 5,
    AnyOf = 6,
    ExactMatch = 7,
    Numeric = 8,
    Comparison = 9,
}

export enum PatternTypes {
    Email = 1,
    PhoneNumber = 2,
    URL = 3,
    IPAddress = 4,
    PostalCode = 5
}

export enum LogicalOperators {

}

export enum ConditionalOperators {
    Equal = 1,
    NotEqual = 2,
    GreaterThan = 3,
    LessThan = 4,
    GreaterThanOrEqualTo = 5,
    LessThanOrEqualTo = 6
}

export enum PageNames {
    Offline_Process = 1,
    Online_Process = 2,
    Generic_RuleList = 3
}

export enum EIBQueueStatus {
    Queued = 1,
    Generating = 2,
    Done = 3,
    Error = 4
}

export enum PDMProfilingStatus {
    running = 1,
    
}