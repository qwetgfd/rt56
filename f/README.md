# DataIngestion

Angular NgModule application scaffold generated from `PROJECT_ARCHITECTURE.md`.

## Quick start

```bash
npm install
npm start
```

Build output: `dist/data-ingestion`

## Project layout

```
src/
├── main.ts
├── index.html
├── styles.css
└── app/
    ├── app.module.ts
    ├── app-routing.module.ts
    ├── app.component.*
    ├── core/
    │   ├── core.module.ts
    │   ├── services/        (20 services)
    │   ├── guards/          (3 guards)
    │   ├── interceptors/    (2 interceptors)
    │   ├── resolvers/       (1 resolver)
    │   └── models/          (26 model interfaces)
    ├── shared/
    │   ├── shared.module.ts
    │   ├── components/      (notification, error-box)
    │   ├── directives/      (nospaces.directive.ts)
    │   ├── pipes/           (pipes.ts)
    │   └── models/          (5 model interfaces)
    ├── environments/
    ├── main-layout/         (main-layout.module.ts)
    ├── eib/                 (lazy EibModule)
    ├── profiling/           (lazy ProfilingModule)
    └── [feature folders]    (33 components per architecture doc)
```

Regenerate component/service stubs:

```bash
python scripts/generate-scaffold.py
```

## Next steps

- Wire MSAL factories in `app.module.ts`
- Implement guard/resolver/interceptor logic
- Add PrimeNG, Material, and chart modules as needed
- Fill in core/shared model interfaces
