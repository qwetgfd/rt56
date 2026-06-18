import { platformBrowserDynamic } from '@angular/platform-browser-dynamic';

import { AppModule } from './app/app.module';
import { SharePointUserAuthService } from './app/sharepoint/services/sharepoint-user.service';
import { environment } from './app/environments/environment';

async function start(): Promise<void> {
  const mode = await SharePointUserAuthService.bootstrapBeforeAngular(environment);
  if (mode === 'popup-only') return;
  await platformBrowserDynamic().bootstrapModule(AppModule);
}

start().catch((err) => console.error(err));
