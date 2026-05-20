import {
  ApplicationDto,
  ApplicationTypeCode,
  ApplicationTypeDto,
  FileKind,
  WorkspaceConnection,
} from './sharepoint.types';

export function parseApiError(err: unknown, fallback: string): string {
  const e = err as { error?: { message?: string }; message?: string };
  return e?.error?.message || e?.message || fallback;
}

export const FALLBACK_APPLICATION_TYPES: ApplicationTypeDto[] = [
  { applicationTypeId: 1, code: 'tp_internal', displayName: 'Internal' },
  { applicationTypeId: 2, code: 'tp_external', displayName: 'External' },
  { applicationTypeId: 3, code: 'custom', displayName: 'Custom' },
];

export function isApplicationTypeCode(code: string): code is ApplicationTypeCode {
  return code === 'tp_internal' || code === 'tp_external' || code === 'custom';
}

export function connectionFromApplication(app: ApplicationDto): WorkspaceConnection {
  return {
    applicationId: app.applicationId,
    tenantId: app.tenantId,
    clientId: app.clientId,
    clientSecret: app.clientSecret,
    consumerClientId: app.consumerClientId ?? '',
    consumerSecret: app.consumerSecret ?? '',
    hostName: app.hostName,
    siteName: app.siteName ?? '',
    libraryName: app.libraryName ?? '',
  };
}

export const FILE_KIND_PATTERNS: Array<{ kind: FileKind; mime?: RegExp; extension: RegExp }> = [
  { kind: 'video', mime: /^video\//, extension: /\.(mp4|m4v|m4a|webm|mov|qt|avi|wmv|asf|mkv|flv|f4v|3gp|3g2|mpg|mpeg|mpe|m1v|m2v|ogv|ogm|ts|m2ts|mts|vob|divx|xvid|hevc|h265|h264|rec)$/i },
  { kind: 'audio', mime: /^audio\//, extension: /\.(mp3|wav|ogg|oga|flac|aac|m4a|wma|opus|weba|aiff|aif|amr|caf|mid|midi)$/i },
  { kind: 'pdf', mime: /pdf/, extension: /\.pdf$/i },
  { kind: 'word', mime: /word|opendocument\.text/, extension: /\.(doc|docx|odt|rtf)$/i },
  { kind: 'csv', mime: /^text\/csv/i, extension: /\.csv$/i },
  { kind: 'excel', mime: /excel|sheet/, extension: /\.(xls|xlsx|ods)$/i },
  { kind: 'powerpoint', mime: /powerpoint|presentation/, extension: /\.(ppt|pptx|odp)$/i },
  { kind: 'image', mime: /^image\//, extension: /\.(png|jpg|jpeg|gif|bmp|webp|svg|ico|tiff)$/i },
  { kind: 'text', mime: /^text\//, extension: /\.(txt|md|json|xml|log|cfg|ini|html|htm|css|js|ts|sh|bat|py|cs|java|sql|yaml|yml)$/i },
];

export function detectFileKind(item: { isFolder: boolean; path?: string; name: string; mimeType?: string }): FileKind {
  if (item.isFolder) return 'folder';
  const name = (item.path ?? item.name).toLowerCase();
  const mime = (item.mimeType ?? '').toLowerCase();
  for (const p of FILE_KIND_PATTERNS) {
    if (p.mime?.test(mime) || p.extension.test(name)) return p.kind;
  }
  return 'unknown';
}

export function sortSharePointItems<T extends { isFolder: boolean; name: string }>(items: T[]): T[] {
  return [...items].sort((a, b) => {
    if (a.isFolder !== b.isFolder) return a.isFolder ? -1 : 1;
    return a.name.localeCompare(b.name, undefined, { sensitivity: 'base' });
  });
}

export class StatusAlerts {
  error = '';
  success = '';
  private timer: ReturnType<typeof setTimeout> | null = null;

  destroy(): void {
    this.cancelTimer();
  }

  dismissSuccess(): void {
    this.cancelTimer();
    this.success = '';
  }

  setSuccess(message: string, dismissMs = 5000): void {
    this.cancelTimer();
    this.error = '';
    this.success = message;
    this.timer = setTimeout(() => this.dismissSuccess(), dismissMs);
  }

  clear(): void {
    this.error = '';
    this.dismissSuccess();
  }

  private cancelTimer(): void {
    if (this.timer != null) {
      clearTimeout(this.timer);
      this.timer = null;
    }
  }
}

export interface CurlModalInput {
  apiBaseUrl: string;
  apiVersion?: string;
  applicationId: string;
  apiSecret: string;
  displayName?: string;
  sampleFolderPath?: string;
  sampleFilePath?: string;
}

export interface CurlStep {
  number: number;
  title: string;
  description?: string;
  command: string;
  optional?: boolean;
}

export interface CurlModalResult {
  baseUrl: string;
  displayName: string;
  apiVersion: string;
  fullText: string;
  steps: CurlStep[];
}

export function buildCurlCommands(input: CurlModalInput): CurlModalResult {
  const base = input.apiBaseUrl.replace(/\/$/, '');
  const apiVersion = input.apiVersion?.trim() || '1.0';
  const name = input.displayName?.trim() || 'SharePoint Application';
  const versionHeader = `-H "x-tpdi-api-version: ${apiVersion}"`;
  const authHeader = `-H "Authorization: Bearer YOUR_ACCESS_TOKEN"`;
  const filePath = input.sampleFilePath ?? 'folder/your-file.mp4';
  const step1 = `curl -X POST "${base}/auth/token" \\\n  ${versionHeader} \\\n  -H "x-application-id: ${input.applicationId}" \\\n  -H "x-client-secret: ${input.apiSecret}"`;
  const step2 = `curl -X GET "${base}/workspace/browse?path=${encodeURIComponent(input.sampleFolderPath ?? '')}" \\\n  ${versionHeader} \\\n  ${authHeader}`;
  const step3 = `curl -X POST "${base}/workspace/file" \\\n  ${versionHeader} \\\n  ${authHeader} \\\n  -H "Content-Type: application/json" \\\n  -d '${JSON.stringify({ filePath })}' \\\n  --output downloaded-file`;
  const steps: CurlStep[] = [
    { number: 1, title: 'Get access token', description: 'Use registered app ID and API secret. Copy accessToken from the response.', command: step1 },
    { number: 2, title: 'Browse folder', description: 'Optional. Replace YOUR_ACCESS_TOKEN with the token from step 1.', command: step2, optional: true },
    { number: 3, title: 'Download or stream file', description: 'Send file path in JSON body. Replace YOUR_ACCESS_TOKEN and filePath as needed.', command: step3 },
  ];
  return {
    baseUrl: base,
    displayName: name,
    apiVersion,
    fullText: `# ${name} — SharePoint Streaming API\n# Base URL: ${base}\n# Header: x-tpdi-api-version: ${apiVersion}\n#\n# Step 1\n${step1}\n\n# Step 2\n${step2}\n\n# Step 3\n${step3}`,
    steps,
  };
}

export function highlightCurl(command: string): string {
  const esc = (s: string) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
  return esc(command)
    .split('\n')
    .map((line) => {
      if (line.trimStart().startsWith('#')) return `<span class="tok-comment">${line}</span>`;
      let out = line;
      out = out.replace(/\bcurl\b/g, '<span class="tok-cmd">curl</span>');
      out = out.replace(/(-X)\s+(\w+)/g, '<span class="tok-flag">$1</span> <span class="tok-method">$2</span>');
      out = out.replace(/(--output)\s+(\S+)/g, '<span class="tok-flag">$1</span> <span class="tok-arg">$2</span>');
      out = out.replace(/(-H)\s+"([^"]*)"/g, '<span class="tok-flag">$1</span> <span class="tok-header">"$2"</span>');
      out = out.replace(/(-d)\s+'([^']*)'/g, '<span class="tok-flag">$1</span> <span class="tok-json">\'$2\'</span>');
      out = out.replace(/"https?:\/\/[^"]+"/g, (m) => `<span class="tok-url">${m}</span>`);
      return out;
    })
    .join('\n');
}

export function curlScriptFilename(displayName: string): string {
  const slug = displayName.trim().toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '') || 'sharepoint-app';
  return `${slug}-streaming-api.txt`;
}

export function downloadCurlScript(content: string, filename: string): void {
  const url = URL.createObjectURL(new Blob([content], { type: 'text/plain;charset=utf-8' }));
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}

export async function copyCurlText(text: string): Promise<boolean> {
  try {
    await navigator.clipboard.writeText(text);
    return true;
  } catch {
    return false;
  }
}

export async function generateCurlCommands(input: CurlModalInput, delayMs = 600): Promise<CurlModalResult> {
  await new Promise((r) => setTimeout(r, delayMs));
  return buildCurlCommands(input);
}
