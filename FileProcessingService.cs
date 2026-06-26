-- GetProcessList(6) = sel_flpConfigurationList. Run line 3 first; use update on line 4 OR line 5 (not both).

select dbo.di_flpConfiguration.flpConfigurationId, dbo.di_flpConfiguration.process_name, dbo.di_flpSchedulerConfiguration.scheduleTypeId, dbo.di_flpSchedulerConfiguration.scheduleStartDate, dbo.di_flpSchedulerConfiguration.scheduleStartTime, dbo.di_flpSchedulerConfiguration.scheduleEndDate, dbo.di_flpSchedulerConfiguration.scheduleEndTime, dbo.di_processScheduler.nextRun from dbo.di_flpConfiguration inner join dbo.di_flpSchedulerConfiguration on dbo.di_flpSchedulerConfiguration.flpConfigurationId = dbo.di_flpConfiguration.flpConfigurationId and dbo.di_flpSchedulerConfiguration.active = 1 left join dbo.di_processScheduler on dbo.di_processScheduler.flpConfigurationId = dbo.di_flpConfiguration.flpConfigurationId and dbo.di_processScheduler.active = 1 where dbo.di_flpConfiguration.processTypeId = 6 and dbo.di_flpConfiguration.flpConfigurationId = 'PUT-FLP-GUID-HERE'

-- scheduleTypeId = 1 -> table di_flpSchedulerConfiguration: set scheduleStartDate + scheduleStartTime (past), scheduleEndDate + scheduleEndTime (future)
update dbo.di_flpSchedulerConfiguration set scheduleStartDate = cast(getutcdate() as date), scheduleStartTime = cast(dateadd(minute, -5, getutcdate()) as time(7)), scheduleEndDate = dateadd(year, 1, cast(getutcdate() as date)), scheduleEndTime = cast('23:59:59' as time(7)), updationDateTime = getutcdate() where flpConfigurationId = 'PUT-FLP-GUID-HERE' and active = 1

-- scheduleTypeId <> 1 -> table di_processScheduler: set nextRun (past)
update dbo.di_processScheduler set nextRun = dateadd(minute, -1, getutcdate()), active = 1 where flpConfigurationId = 'PUT-FLP-GUID-HERE' and active = 1

select dbo.di_flpConfiguration.flpConfigurationId, dbo.di_flpConfiguration.process_name, dbo.di_flpSharePointSource.sharePointApplicationId, dbo.di_flpSharePointSource.sharePointLibraryName, dbo.di_flpSharePointSource.sharePointFolderPath from dbo.di_flpConfiguration inner join dbo.di_flpSharePointSource on dbo.di_flpSharePointSource.flpConfigurationId = dbo.di_flpConfiguration.flpConfigurationId and dbo.di_flpSharePointSource.active = 1 where dbo.di_flpConfiguration.processTypeId = 6 and dbo.di_flpConfiguration.flpConfigurationId = 'PUT-FLP-GUID-HERE'

exec dbo.sel_flpConfigurationList @processTypeId = 6

select uploadFileId, fileName, flpConfigurationId, flpProcessStatusId, CreationDateTime from dbo.di_uploadedFile where active = 1 and processTypeId = 6 and flpConfigurationId = 'PUT-FLP-GUID-HERE' order by CreationDateTime desc

exec dbo.sel_FileUploadStatusByProcessId @flpConfigurationId = 'PUT-FLP-GUID-HERE'

exec dbo.sel_GetDetailedFileUploadStatus @flpConfigurationId = 'PUT-FLP-GUID-HERE', @uploadFileId = 'PUT-UPLOAD-GUID-HERE'
