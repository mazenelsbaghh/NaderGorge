package main

import (
	"bytes"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"net/http"
	"time"
)

type maintenanceAuthorization struct {
	deadline int64
	release  func() error
}

func networkMaintenance(request input) (*maintenanceAuthorization, error) {
	if request.AdminPort == 0 {
		request.AdminPort = 8766
	}
	if request.AdminPort != 8766 || len(request.AdminToken) < 32 || len(request.AdminToken) > 256 {
		return nil, errors.New("افتح قسم الامتحانات أولًا لتأكيد إغلاق القاعات قبل تعديل الشبكة.")
	}
	client := &http.Client{Timeout: 5 * time.Second, Transport: &http.Transport{Proxy: nil}, CheckRedirect: func(*http.Request, []*http.Request) error { return http.ErrUseLastResponse }}
	call := func(action, lease string) (map[string]any, error) {
		payload, _ := json.Marshal(map[string]string{"leaseToken": lease})
		command, err := http.NewRequest(http.MethodPost, fmt.Sprintf("http://127.0.0.1:%d/api/network/maintenance/%s", request.AdminPort, action), bytes.NewReader(payload))
		if err != nil {
			return nil, err
		}
		command.Header.Set("Content-Type", "application/json")
		command.Header.Set("X-Exam-Request", "1")
		command.Header.Set("X-Admin-Token", request.AdminToken)
		response, err := client.Do(command)
		if err != nil {
			return nil, errors.New("خادم الامتحانات لا يستجيب؛ لم يتم تغيير الشبكة.")
		}
		defer response.Body.Close()
		var decoded map[string]any
		err = json.NewDecoder(io.LimitReader(response.Body, 8192)).Decode(&decoded)
		if response.StatusCode != 200 || err != nil {
			return nil, errors.New("لا يمكن تغيير الشبكة قبل إغلاق القاعات المفتوحة وتأكيد خادم الامتحانات.")
		}
		return decoded, nil
	}
	acquired, err := call("acquire", "")
	if err != nil {
		return nil, err
	}
	token, ok := acquired["leaseToken"].(string)
	deadline, valid := acquired["expiresAt"].(float64)
	if !ok || len(token) < 32 || !valid || deadline < float64(time.Now().Add(4*time.Minute).Unix()) {
		return nil, errors.New("تصريح صيانة الشبكة غير صالح.")
	}
	return &maintenanceAuthorization{deadline: int64(deadline), release: func() error { _, err := call("release", token); return err }}, nil
}
