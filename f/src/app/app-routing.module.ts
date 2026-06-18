import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

import { LoginComponent } from './login/login.component';
import { MainLayoutComponent } from './main-layout/main-layout.component';
import { UnauthorizedComponent } from './unauthorized/unauthorized.component';
import { FileUploadComponent } from './file-upload/file-upload.component';
import { FileProcessingStatusComponent } from './file-processing-status/file-processing-status.component';
import { DashboardComponent } from './dashboard/dashboard.component';
import { DatasourcesComponent } from './datasources/datasources.component';
import { UserGroupComponent } from './user-group/user-group.component';
import { ProcessConfigurationComponent } from './process-configuration/process-configuration.component';
import { AddProcessComponent } from './add-process/add-process.component';
import { ProcessConfigListComponent } from './process-config-list/process-config-list.component';
import { ProcessedFileListComponent } from './processed-file-list/processed-file-list.component';
import { RulesetListComponent } from './ruleset-list/ruleset-list.component';
import { CreateEibComponent } from './eib/create-eib/create-eib.component';
import { addEIBAccessGuard } from './core/guards/add-eib-access.guard';
import { OfflineModuleResolver } from './core/resolvers/offline-module.resolver';
// #region Sharepoint Workspace - AY
import { SharepointComponent } from './sharepoint/sharepoint.component';
// #endregion

const mainLayoutPaths = [
  'file-upload',
  'file-processing-status',
  'dashboard',
  'add',
  'user-group',
  'process-configuration',
  'add-process',
  'process-config-list',
  'processed-file-list',
  'ruleset-list',
  // #region Sharepoint Workspace - AY
  'ingestion-console',
  // #endregion
];

const mainLayoutRedirects: Routes = mainLayoutPaths.map((path) => ({
  path,
  redirectTo: `mainlayout/${path}`,
  pathMatch: 'full' as const,
}));

const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: 'login', component: LoginComponent },
  { path: 'unauthorized', component: UnauthorizedComponent },
  ...mainLayoutRedirects,
  {
    path: 'mainlayout',
    component: MainLayoutComponent,
    children: [
      { path: 'file-upload', component: FileUploadComponent },
      { path: 'file-processing-status', component: FileProcessingStatusComponent },
      { path: 'file-processing-status/:id', component: FileProcessingStatusComponent },
      { path: 'dashboard', component: DashboardComponent },
      { path: 'add', component: DatasourcesComponent },
      { path: 'user-group', component: UserGroupComponent },
      { path: 'process-configuration', component: ProcessConfigurationComponent },
      {
        path: 'add-process',
        component: AddProcessComponent,
        resolve: { lookups: OfflineModuleResolver },
      },
      { path: 'process-config-list', component: ProcessConfigListComponent },
      {
        path: 'process-configuration/:id',
        component: ProcessConfigurationComponent,
        resolve: { lookups: OfflineModuleResolver },
      },
      {
        path: 'process-configuration/:id/:tabName',
        component: ProcessConfigurationComponent,
        resolve: { lookups: OfflineModuleResolver },
      },
      { path: 'processed-file-list', component: ProcessedFileListComponent },
      { path: 'ruleset-list', component: RulesetListComponent },
      // #region Sharepoint Workspace - AY
      { path: 'ingestion-console', component: SharepointComponent },
      // #endregion
    ],
  },
  {
    path: 'create-eib',
    component: CreateEibComponent,
    canActivate: [addEIBAccessGuard],
  },
  {
    path: 'eib',
    loadChildren: () => import('./eib/eib.module').then((m) => m.EibModule),
  },
  {
    path: 'profiling',
    loadChildren: () =>
      import('./profiling/profiling.module').then((m) => m.ProfilingModule),
  },
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule],
})
export class AppRoutingModule {}
