import axios, { Axios } from "axios";
import { HttpCrisEndpoint } from "@signature-code/ck-observable-domain-mqtt/dist/services/http-cris-endpoint";
import { MqttObservableLeagueDomainService } from "@signature-code/ck-observable-domain-mqtt/dist/mqtt-od-league-driver"
import { ObservableDomainClient } from "@signature-code/ck-observable-domain-mqtt/dist/observable-domain-client";
import { SliderCommand } from "@signature/generated";

const crisAxios = new Axios({
    baseURL: "http://localhost:5000/.cris"
});
const crisEndpoint = new HttpCrisEndpoint(crisAxios);

export async function startPage(): Promise<void> {

    const odDriver = new MqttObservableLeagueDomainService("ws://localhost:8080/mqtt/", crisEndpoint, "CK-OD");
    const odClient = new ObservableDomainClient(odDriver);
    odClient.start();
    const odObs = await odClient.listenToDomain("Test-Domain");

    const left = document.getElementById("slider-left") as HTMLInputElement;
    const right = document.getElementById("slider-right");

    odObs.forEach((s) =>  {;
        console.log(s);
        if(s.length < 1) return;
        left.value = s[0].Slider
    });
}

export async function sliderUpdate(sliderValue): Promise<void> {
    crisEndpoint.send<void>(SliderCommand.create(Number(sliderValue)));
}


startPage();

window["sliderUpdate"] = sliderUpdate;
