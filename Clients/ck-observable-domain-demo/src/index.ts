import axios, { Axios } from "axios";
import { HttpCrisEndpoint } from "@signature-code/ck-observable-domain-mqtt/dist/services/http-cris-endpoint";
import { MqttObservableLeagueDomainService } from "@signature-code/ck-observable-domain-mqtt/dist/mqtt-od-league-driver"
import { ObservableDomainClient } from "@signature-code/ck-observable-domain-mqtt/dist/observable-domain-client";



export async function startPage(): Promise<void> {
    const crisAxios = new Axios({
        baseURL: "http://localhost:5000/.cris"
    });
    const crisEndpoint = new HttpCrisEndpoint(crisAxios);
    const odDriver = new MqttObservableLeagueDomainService("ws://localhost:8080/mqtt/", crisEndpoint, "CK-OD");
    const odClient = new ObservableDomainClient(odDriver);
    await odClient.start();
    await odClient.listenToDomain("Test-Domain")

    const left = document.getElementById("slider-left");
    const right = document.getElementById("slider-right");

    const onChange = (ev) => {
        console.log(ev)
    }
    left.onchange = onChange;
    right.onchange = onChange;
}


startPage();