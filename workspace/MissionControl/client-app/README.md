# ClientApp

This project was generated using [Angular CLI](https://github.com/angular/angular-cli) version 21.2.5.

## Development server

To start a local development server that is accessible from your machine and network (including Tailscale/mobile):

```bash
npm start -- --host 0.0.0.0
```

- On your computer, go to [http://localhost:4200](http://localhost:4200)
- From your phone or any other device on the same network or Tailscale, go to:
  - `http://<your-computer-ip>:4200` (e.g., http://192.168.1.74:4200)
  - Or via Tailscale: `http://<Your-Tailscale-IP>:4200` (e.g., http://100.100.218.48:4200)

> Ensure your firewall/Tailscale ACLs allow access to port 4200.

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

To build the project run:

```bash
ng build
```

This will compile your project and store the build artifacts in the `dist/` directory. By default, the production build optimizes your application for performance and speed.

## Running unit tests

To execute unit tests with [Vitest](https://vitest.dev/), use the following command:

```bash
ng test
```

## Running end-to-end tests

For end-to-end (e2e) testing, run:

```bash
ng e2e
```

Angular CLI does not come with an end-to-end testing framework by default. You can choose one that suits your needs.

## Additional Resources

For more information on using the Angular CLI, including detailed command references, visit the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.
