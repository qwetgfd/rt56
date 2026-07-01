/* block 1 — utc now (paste into @utc / @end in block 5) */
select getutcdate();

/* block 2 — sharepoint configs due for getprocesslist(6) right now (processTypeId=6, locationTypeId=4) */
select flp.flpConfigurationId, flp.process_name, flp.clientId, flp.search_string_in_file_name,
  coalesce(flp.sharePointApplicationId, sp.sharePointApplicationId) as sharePointApplicationId,
  coalesce(flp.sharePointApplicationSiteId, sp.sharePointApplicationSiteId) as sharePointApplicationSiteId,
  coalesce(flp.sharePointLibraryName, sp.sharePointLibraryName) as sharePointLibraryName,
  coalesce(flp.sharePointFolderPath, sp.sharePointFolderPath, flp.sourcePath) as sharePointFolderPath,
  sc.scheduleTypeId, ps.nextRun
from dbo.di_flpConfiguration flp
inner join dbo.di_flpSchedulerConfiguration sc on sc.flpConfigurationId = flp.flpConfigurationId and sc.active = 1
left join dbo.di_processScheduler ps on ps.flpConfigurationId = flp.flpConfigurationId and ps.active = 1
left join dbo.di_flpSharePointSource sp on sp.flpConfigurationId = flp.flpConfigurationId and sp.active = 1
where flp.is_active = 1 and flp.processTypeId = 6 and flp.locationTypeId = 4 and flp.fileProcessingServerTypeId in (1, 2, 4)
  and (sc.scheduleEndDate is null or cast(sc.scheduleEndDate as datetime) + cast(sc.scheduleEndTime as datetime) >= getutcdate())
  and ((sc.scheduleTypeId <> 1 and ps.nextRun <= getutcdate()) or (sc.scheduleTypeId = 1 and cast(sc.scheduleStartDate as datetime) + cast(sc.scheduleStartTime as datetime) <= getutcdate()))
order by flp.process_name;

/* block 3 — find one config by process name (copy flpConfigurationId + scheduleTypeId) */
declare @processName nvarchar(255) = 'PUT-PROCESS-NAME-HERE';

select dbo.di_flpConfiguration.flpConfigurationId, dbo.di_flpConfiguration.process_name, dbo.di_flpConfiguration.processTypeId, dbo.di_flpConfiguration.locationTypeId,
  dbo.di_flpSchedulerConfiguration.scheduleTypeId, dbo.di_processScheduler.nextRun
from dbo.di_flpConfiguration
inner join dbo.di_flpSchedulerConfiguration on dbo.di_flpSchedulerConfiguration.flpConfigurationId = dbo.di_flpConfiguration.flpConfigurationId and dbo.di_flpSchedulerConfiguration.active = 1
left join dbo.di_processScheduler on dbo.di_processScheduler.flpConfigurationId = dbo.di_flpConfiguration.flpConfigurationId and dbo.di_processScheduler.active = 1
where dbo.di_flpConfiguration.is_active = 1 and dbo.di_flpConfiguration.process_name = @processName;

/* block 4 — all active sharepoint configs (processTypeId=6, locationTypeId=4) */
select dbo.di_flpConfiguration.flpConfigurationId, dbo.di_flpConfiguration.process_name, dbo.di_flpConfiguration.processTypeId, dbo.di_flpConfiguration.locationTypeId,
  dbo.di_flpSchedulerConfiguration.scheduleTypeId, dbo.di_processScheduler.nextRun
from dbo.di_flpConfiguration
inner join dbo.di_flpSchedulerConfiguration on dbo.di_flpSchedulerConfiguration.flpConfigurationId = dbo.di_flpConfiguration.flpConfigurationId and dbo.di_flpSchedulerConfiguration.active = 1
left join dbo.di_processScheduler on dbo.di_processScheduler.flpConfigurationId = dbo.di_flpConfiguration.flpConfigurationId and dbo.di_processScheduler.active = 1
where dbo.di_flpConfiguration.is_active = 1 and dbo.di_flpConfiguration.processTypeId = 6 and dbo.di_flpConfiguration.locationTypeId = 4;

/* block 5 — force one config due: set @id from block 3/4, paste block 1 into @utc/@end, run matching update only */
declare @id varchar(36) = 'PUT-FLP-GUID-HERE', @utc datetime = 'paste-getutcdate-here', @end datetime = 'paste-future-end-here';

update dbo.di_flpSchedulerConfiguration set scheduleStartDate = @utc, scheduleStartTime = @utc, scheduleEndDate = @end, scheduleEndTime = @end, updationDateTime = @utc where flpConfigurationId = @id and active = 1 and scheduleTypeId = 1;

update dbo.di_processScheduler set nextRun = @utc, active = 1 where flpConfigurationId = @id and active = 1
  and exists (select 1 from dbo.di_flpSchedulerConfiguration where flpConfigurationId = @id and active = 1 and scheduleTypeId <> 1);
