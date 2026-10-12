package main

import (
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"os"
)

type input struct {
	MaintenanceDeadline int64  `json:"maintenanceDeadline,omitempty"`
	Action              string `json:"action"`
	AdapterID           string `json:"adapterId"`
	AdminPort           int    `json:"adminPort"`
	AdminToken          string `json:"adminToken"`
	SSID                string `json:"ssid"`
	Password            string `json:"password"`
	Mode                string `json:"mode"`
	Hostname            string `json:"hostname"`
	CertificatePath     string `json:"certificatePath"`
	PrivateKeyPath      string `json:"privateKeyPath"`
}
type output map[string]any

func main() {
	if len(os.Args) > 1 && os.Args[1] == "run" {
		if err := runNetwork(); err != nil {
			fmt.Fprintln(os.Stderr, "network service stopped:", err)
			os.Exit(1)
		}
		return
	}
	if len(os.Args) > 1 && os.Args[1] == "apply" {
		applyElevated()
		return
	}
	request, err := readInput(os.Stdin)
	var response output
	if err != nil {
		response = output{"ok": false, "message": "تعذر قراءة طلب تجهيز الشبكة."}
	} else {
		response = dispatch(request)
	}
	response["schemaVersion"] = 1
	response["physicalWindowsVerified"] = false
	json.NewEncoder(os.Stdout).Encode(response)
	if response["ok"] == false {
		os.Exit(1)
	}
}
func readInput(source io.Reader) (input, error) {
	var request input
	decoder := json.NewDecoder(io.LimitReader(source, 16385))
	decoder.DisallowUnknownFields()
	if err := decoder.Decode(&request); err != nil {
		return request, err
	}
	var extra any
	if decoder.Decode(&extra) != io.EOF {
		return request, errors.New("multiple requests")
	}
	if request.Action == "" && len(os.Args) > 1 {
		request.Action = os.Args[1]
	}
	return request, nil
}
func dispatch(request input) output {
	switch request.Action {
	case "status", "list-adapters":
		return networkInspection(request.Action)
	case "configure", "restore", "portal-configure":
		authorization, err := networkMaintenance(request)
		if err != nil {
			return output{"ok": false, "message": err.Error()}
		}
		request.MaintenanceDeadline = authorization.deadline
		response := changeNetwork(request)
		if err = authorization.release(); err != nil {
			response["maintenanceWarning"] = "تعذر إنهاء حجز التهيئة؛ انتظر عشر دقائق قبل فتح قاعة."
		}
		return response
	case "hotspot-status", "hotspot-start", "hotspot-stop":
		return hotspot(request)
	default:
		return output{"ok": false, "message": "أمر الشبكة غير معروف."}
	}
}
