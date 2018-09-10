# CK-Observable-Domain client implementation

## Usage

In your `.npmrc`, next to your `package.json`:

```
@signature:registry=https://invenietis.pkgs.visualstudio.com/_packaging/InternalNPM/npm/registry
@signature:always-auth=true
```

In a shell, next to your `package.json`:

```sh
npm i -g vsts-npm-auth
vsts-npm-auth -C .npmrc
npm i @signature/ck-observable-domain
```

In your favorite TS script:

```ts
import { ObservableDomain, ObservableDomainEvent, ObservableDomainState } from "@signature/ck-observable-domain";

const initialState: ObservableDomainState = myStateRepository.getCurrentState();
// initialState can also be a string; it will be deserialized using @signature/json-graph-serializer.
const observableDomain: ObservableDomain = new ObservableDomain(initial);

// Events can't be strings, however, but you can deserialize then using JSON.parse().
myStateRepository.onStateEvent((evt: ObservableDomainEvent) => {
    observableDomain.applyEvent(evt);
    // If you're missing events (evt.N > (observableDomain.transactionNumber + 1)),
    // you'll have to get and apply those by yourself before appplying new ones.
});

// You can find your root objects in observableDomain.roots.
// If you have only one, it should be observableDomain.roots[0].
```
