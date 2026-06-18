# File First — SharePoint Workspace File Selection (Kaise Kaam Karta Hai)

## Pehle samjho kya kiya hai

SharePoint workspace already ek alag component tha — `SharepointWorkspaceComponent`. Usme poora browse/connect/preview logic hai.

File First section ke liye humne **us workspace ko directly file-upload mein nahi daala**. Ek **alag wrapper component** banaya:

- **`FileFirstAttachComponent`** (`src/app/file-first/file-first-attach.component.ts`)

Is wrapper ka kaam sirf yeh hai:
1. Andar `app-sharepoint-workspace` embed karna
2. Workspace ko **file pick mode** mein chalana (`[filePickMode]="true"`)
3. User jab file select kare, blob download karke `File` object banana
4. Parent (`FileUploadComponent`) ko `fileSelected` event se file dena

Matlab — SharePoint workspace **separately bind** hai File First section pe, ek dedicated component ke through.

---

## Kahan bind hua hai

### Step 1 — File Upload page pe toggle

`file-upload.component.html` mein user **"SharePoint Workspace"** button dabata hai:

```
(click)="toggleFileSource('sharepoint')"
  → FileUploadComponent.toggleFileSource('sharepoint')
  → showSharepoint = true
```

Jab `showSharepoint` true hota hai, local upload form hide hota hai aur yeh render hota hai:

```html
<app-file-first-attach
  (fileSelected)="onSharepointFileSelected($event)"
  (back)="toggleFileSource('local')">
</app-file-first-attach>
```

Yahan se File First ka alag section start hota hai.

---

### Step 2 — FileFirstAttachComponent andar workspace lagata hai

`file-first-attach.component.html`:

```html
<app-sharepoint-workspace
  [filePickMode]="true"
  [pickFileBusy]="fileLoading"
  (pickFile)="useSelectedFile()"
  (viewerOpenChange)="onViewerOpenChange($event)"
  (navigateHome)="goBack()"
  (navigateApplications)="goBack()">
</app-sharepoint-workspace>
```

Yeh important hai:
- **`[filePickMode]="true"`** — workspace ko batata hai ki yahan file pick karni hai, normal standalone mode nahi
- **`(pickFile)`** — viewer mein "Select this file" dabane pe `useSelectedFile()` call hoga
- **`(viewerOpenChange)`** — preview khula/banda — isse neeche wali selection bar show/hide hoti hai

`FileFirstAttachComponent` workspace ko `@ViewChild` se access bhi karta hai taaki `selectedItem` padh sake:

```typescript
@ViewChild(SharepointWorkspaceComponent)
protected workspace!: SharepointWorkspaceComponent;
```

---

## File select ka poora flow (function by function)

### 1. User folder/file list mein click karta hai

Template (`sharepoint-workspace.component.html`):

```
(click)="openItem(item)"
(dblclick)="filePickMode && openItemPreview(item)"
```

**Single click** → `SharepointWorkspaceComponent.openItem(item)`

```typescript
openItem(item) {
  if (item.isFolder) {
    loadChildren(item.path ?? item.name);   // folder ke andar jao
    return;
  }
  if (this.filePickMode) {
    this.selectedItem = item;               // sirf select karo, preview mat kholo
    return;
  }
  openItemPreview(item);                    // normal mode mein preview khulta hai
}
```

File First mode mein single click sirf `selectedItem` set karta hai.

---

**Double click** → `SharepointWorkspaceComponent.openItemPreview(item)`

```typescript
openItemPreview(item) {
  if (item.isFolder) { loadChildren(...); return; }

  this.selectedItem = item;
  this.showViewer = true;
  this.viewerOpenChange.emit(true);   // parent ko bataya — viewer khul gaya
  // ... file preview load hoti hai
}
```

Double click pe file select + preview dono hote hain.

---

### 2. Selection UI dikhta hai

`FileFirstAttachComponent` workspace ka `selectedItem` read karta hai:

```typescript
get selectedItem() {
  return this.workspace?.selectedItem ?? null;
}

get hasFileSelected() {
  return selectedItem != null && !selectedItem.isFolder && !fileLoading;
}
```

**Do jagah "Select this file" button hai:**

| Jagah | Kab dikhega | Click pe kya call hoga |
|-------|-------------|------------------------|
| Neeche selection bar (`ff-selection`) | File select hai + viewer band hai | `useSelectedFile()` |
| Viewer footer (`sp-viewer__pick`) | Preview khula hai + `filePickMode` | `pickFile.emit()` → parent pe `(pickFile)="useSelectedFile()"` |

Viewer khula hai ya nahi — yeh `onViewerOpenChange(open)` track karta hai:

```typescript
onViewerOpenChange(open: boolean) {
  this._viewerOpen = open;
}
```

Isliye viewer open hone pe neeche wali bar hide ho jati hai, viewer ke andar wala button dikhta hai.

---

### 3. User "Select this file" dabata hai

Dono buttons same function pe jaate hain → **`FileFirstAttachComponent.useSelectedFile()`**

```typescript
useSelectedFile() {
  const item = this.workspace?.selectedItem;
  if (!item || item.isFolder) return;

  this.workspace?.closeViewer();        // viewer band, selectedItem clear
  this.fileLoading = true;

  const filePath = item.path ?? item.name;

  // mode ke hisaab se API call
  if (this.workspace?.isModeUser) {
    blob$ = userApi.fetchFileBlob(driveId, filePath);
  } else {
    blob$ = api.fetchFileBlob(workspaceConnection, filePath);
  }

  blob$.subscribe({
    next: (blob) => {
      const file = new File([blob], item.name);
      this.fileSelected.emit(file);     // parent ko file mil gayi
    },
    error: () => { this.error = 'Could not download...'; }
  });
}
```

Yahan pe selection complete hoti hai — SharePoint se file download hoke `File` object ban jata hai.

---

### 4. Parent ko file milti hai

`file-upload.component.html`:

```
(fileSelected)="onSharepointFileSelected($event)"
```

→ `FileUploadComponent.onSharepointFileSelected(file)`  
→ wahan se `onFileChange({ target: { files: [file] } })` call hota hai (local file jaisa treat)

---

## Short summary — kaun kya karta hai

```
FileUploadComponent
  toggleFileSource('sharepoint')     → showSharepoint = true
  ↓ renders
FileFirstAttachComponent             → alag component, sirf SharePoint pick ke liye
  ↓ embeds
SharepointWorkspaceComponent         → browse + select + preview
  openItem()                         → single click → selectedItem set
  openItemPreview()                  → double click → selectedItem + viewer
  pickFile.emit()                    → viewer se confirm
  ↓
FileFirstAttachComponent
  useSelectedFile()                  → blob download → File banao → fileSelected.emit()
  ↓
FileUploadComponent
  onSharepointFileSelected(file)     → normal upload flow mein daal do
```

---

## Kyun alag component banaya

1. **`SharepointWorkspaceComponent`** reusable hai — standalone SharePoint page pe bhi chal sakta hai, File First pe bhi
2. **`filePickMode`** input se same workspace alag behaviour deta hai (single click = select only, extra toolbar buttons hide)
3. **`FileFirstAttachComponent`** sirf File First ka glue hai — workspace embed, selection confirm, blob → File, parent ko event
4. **`FileUploadComponent`** clean rehta hai — sirf toggle + event receive, SharePoint logic usme nahi ghusa

---

## Files yaad rakhne wali

| File | Kaam |
|------|------|
| `file-upload.component.html` | Toggle + `<app-file-first-attach>` render |
| `file-upload.component.ts` | `toggleFileSource()`, `onSharepointFileSelected()` |
| `file-first-attach.component.html` | Workspace embed + selection bar |
| `file-first-attach.component.ts` | `useSelectedFile()`, `@ViewChild` workspace |
| `sharepoint-workspace.component.ts` | `openItem()`, `openItemPreview()`, `selectedItem`, `closeViewer()` |
| `sharepoint-workspace.component.html` | File list clicks, viewer footer `pickFile.emit()` |
| `sharepoint-api.service.ts` | `fetchFileBlob()` — app mode |
| `sharepoint-user.service.ts` | `fetchFileBlob()` — user delegated mode |
