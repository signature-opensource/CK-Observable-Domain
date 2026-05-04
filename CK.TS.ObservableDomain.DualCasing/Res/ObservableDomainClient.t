create <ts> transformer on "CK/ObservableDomain/ObservableDomainClient.ts"
begin
    ensure import { setDeprecationWarningsEnabled } from "./DualCasingProxy";
    ensure import { IObservableDomainClientConfiguration } from "./IObservableDomainClientConfiguration";

    replace single """
        constructor(
            private readonly connection: IObservableDomainConnection
        ) {
            this.connection.onMessage(this.onMessage);
            this.connection.onClose(this.onClose);
        }
    """
        with """
        /**
         * @param connection The connection to the OD server.
         * @param options Optional client configuration.
         *   - `suppressDeprecationWarnings`: when true, the global PascalCase deprecation
         *     warning is silenced. Note: this is a module-global toggle. If multiple
         *     clients are constructed with conflicting flags, the last one wins. In
         *     practice every host process owns a single client.
         */
        constructor(
            private readonly connection: IObservableDomainConnection,
            options?: IObservableDomainClientConfiguration
        ) {
            if (options?.suppressDeprecationWarnings) {
                setDeprecationWarningsEnabled(false);
            }
            this.connection.onMessage(this.onMessage);
            this.connection.onClose(this.onClose);
        }
    """;
end
