An observable framework for application states: an ObservableDomain holds ObservableObject graphs (from ObservableRootObject roots) that are modified in transactions, whose events can be exported to remote clients and that are serialized with CK.BinarySerialization.

Main types: ObservableDomain (and its typed versions with up to 4 roots), ObservableObject, ObservableRootObject, IObservableDomainClient, TransactionResult and ObservableDomainSidekick.

See the [README](https://github.com/signature-opensource/CK-Observable-Domain/blob/stable/README.md).
