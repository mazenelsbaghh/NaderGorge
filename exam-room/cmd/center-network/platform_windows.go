//go:build windows

package main

import (
	"bytes"
	_ "embed"
	"encoding/base64"
	"encoding/binary"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"net"
	"net/http"
	"os"
	"os/exec"
	"path/filepath"
	"regexp"
	"strings"
	"syscall"
	"time"
	"unicode/utf16"
	"unsafe"

	"golang.org/x/sys/windows"
)

//go:embed windows-network.ps1
var networkScript string
var adapterGUID = regexp.MustCompile(`^[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}$`)

func replaceFile(source, destination string) error {
	from, err := windows.UTF16PtrFromString(source)
	if err != nil {
		return err
	}
	to, err := windows.UTF16PtrFromString(destination)
	if err != nil {
		return err
	}
	return windows.MoveFileEx(from, to, windows.MOVEFILE_REPLACE_EXISTING|windows.MOVEFILE_WRITE_THROUGH)
}
func psLiteral(value string) string { return "'" + strings.ReplaceAll(value, "'", "''") + "'" }
func powershellEnvironment(inherited []string, executable string) []string {
	environment := make([]string, 0, len(inherited)+1)
	for _, entry := range inherited {
		name, _, _ := strings.Cut(entry, "=")
		if !strings.EqualFold(name, "PSModulePath") {
			environment = append(environment, entry)
		}
	}
	// A parent pwsh 7 process can otherwise force PS5.1 to load incompatible modules.
	return append(environment, "PSModulePath="+filepath.Join(filepath.Dir(executable), "Modules"))
}
func powershell(script string) ([]byte, error) {
	systemDirectory, err := windows.GetSystemDirectory()
	if err != nil {
		return nil, err
	}
	executable := filepath.Join(systemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe")
	// The complete setup exceeds CreateProcess's command-line limit once encoded.
	// Keep only the small loader in argv, with UTF-8 script bytes sent as ASCII stdin.
	const loader = "$env:PSModulePath=[IO.Path]::Combine($PSHOME,'Modules'); $ErrorActionPreference='Stop'; [Console]::OutputEncoding=[Text.UTF8Encoding]::new($false); $bytes=[Convert]::FromBase64String([Console]::In.ReadToEnd()); & ([scriptblock]::Create([Text.Encoding]::UTF8.GetString($bytes)))"
	words := utf16.Encode([]rune(loader))
	payload := make([]byte, len(words)*2)
	for i, word := range words {
		binary.LittleEndian.PutUint16(payload[i*2:], word)
	}
	command := exec.Command(executable, "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand", base64.StdEncoding.EncodeToString(payload))
	command.Env = powershellEnvironment(os.Environ(), executable)
	command.SysProcAttr = &syscall.SysProcAttr{HideWindow: true}
	command.Stdin = strings.NewReader(base64.StdEncoding.EncodeToString([]byte(script)))
	return command.Output()
}
func invokeScriptRequest(request input) (output, error) {
	exe, err := os.Executable()
	if err != nil {
		return nil, err
	}
	prefix := "$MaintenanceDeadline=" + fmt.Sprint(request.MaintenanceDeadline) + "\n$Action=" + psLiteral(request.Action) + "\n$AdapterId=" + psLiteral(request.AdapterID) + "\n$PortalMode=" + psLiteral(request.Mode) + "\n$Hostname=" + psLiteral(request.Hostname) + "\n$CertificatePath=" + psLiteral(request.CertificatePath) + "\n$PrivateKeyPath=" + psLiteral(request.PrivateKeyPath) + "\n$SourceDirectory=" + psLiteral(filepath.Dir(exe)) + "\n"
	content, err := powershell(prefix + networkScript)
	if err != nil {
		return nil, errors.New("تعذر إكمال إعداد ويندوز؛ لم يتم تأكيد تغيير الشبكة.")
	}
	var response output
	if err = json.Unmarshal(bytes.TrimPrefix(content, []byte{0xef, 0xbb, 0xbf}), &response); err != nil {
		return nil, errors.New("نتيجة تجهيز ويندوز غير مقروءة.")
	}
	return response, nil
}
func invokeScript(action, adapter string) (output, error) {
	return invokeScriptRequest(input{Action: action, AdapterID: adapter})
}
func networkInspection(action string) output {
	response, err := invokeScript(action, "")
	if err != nil {
		return output{"ok": false, "supported": true, "message": err.Error()}
	}
	if action == "status" && response["configured"] == true {
		response["dnsReady"] = probeDNS()
		response["portalReady"] = probePortal()
		response["studentServiceReady"] = probeStudentService()
		response["httpsReady"] = false
		if response["portalMode"] == "https" {
			executable, _ := os.Executable()
			check := exec.Command(filepath.Join(filepath.Dir(executable), "massar-exam-portal-proxy.exe"), "--check-portal", "--capport-hostname", fmt.Sprint(response["hostname"]))
			check.SysProcAttr = &syscall.SysProcAttr{HideWindow: true}
			response["httpsReady"] = check.Run() == nil
			if response["httpsReady"] == true {
				response["studentUrl"] = "https://" + fmt.Sprint(response["hostname"]) + "/"
			}
		}
		heartbeatPath := filepath.Join(os.Getenv("ProgramData"), "Massar", "ExamNetwork", "heartbeat.json")
		var heartbeat struct {
			At        int64  `json:"at"`
			AdapterID string `json:"adapterId"`
			DHCP      bool   `json:"dhcp"`
		}
		content, readErr := os.ReadFile(heartbeatPath)
		if readErr == nil {
			readErr = json.Unmarshal(content, &heartbeat)
		}
		age := time.Now().Unix() - heartbeat.At
		response["dhcpReady"] = readErr == nil && age >= 0 && age <= 15 && heartbeat.DHCP && strings.EqualFold(heartbeat.AdapterID, fmt.Sprint(response["adapterId"]))
		response["ready"] = response["addressReady"] == true && response["dhcpReady"] == true && response["dnsReady"] == true && response["portalReady"] == true && response["studentServiceReady"] == true && (response["portalMode"] != "https" || response["httpsReady"] == true)
	}
	return response
}
func changeNetwork(request input) output {
	if request.Action == "portal-configure" {
		state := networkInspection("status")
		if state["configured"] != true {
			return output{"ok": false, "message": "جهّز كابل Omada أولًا."}
		}
		request.AdapterID = fmt.Sprint(state["adapterId"])
	}
	request.AdapterID = strings.Trim(request.AdapterID, "{}")
	if request.Action != "restore" && !adapterGUID.MatchString(request.AdapterID) {
		return output{"ok": false, "message": "اختَر منفذ Ethernet المتصل بالـOmada."}
	}
	if request.Mode != "" && request.Mode != "legacy" && request.Mode != "https" {
		return output{"ok": false, "message": "طريقة الدخول غير معروفة."}
	}
	if request.Mode == "https" && (len(request.Hostname) > 200 || !regexp.MustCompile(`^[a-zA-Z0-9][a-zA-Z0-9.-]*\.[a-zA-Z0-9]+$`).MatchString(request.Hostname) || request.CertificatePath == "" || request.PrivateKeyPath == "") {
		return output{"ok": false, "message": "اختَر اسم نطاق وشهادة ومفتاحًا مطابقًا قبل تفعيل HTTPS."}
	}
	executable, err := os.Executable()
	if err != nil {
		return output{"ok": false, "message": "تعذر تحديد ملف تهيئة الشبكة."}
	}
	// Elevation receives only file paths and network choices. Staff tokens and Wi-Fi passwords never leave stdin.
	clean := input{MaintenanceDeadline: request.MaintenanceDeadline, Action: request.Action, AdapterID: request.AdapterID, Mode: request.Mode, Hostname: request.Hostname, CertificatePath: request.CertificatePath, PrivateKeyPath: request.PrivateKeyPath}
	encoded, _ := json.Marshal(clean)
	arguments := "apply " + base64.RawURLEncoding.EncodeToString(encoded)
	script := "$ErrorActionPreference='Stop'; $p=Start-Process -FilePath " + psLiteral(executable) + " -ArgumentList " + psLiteral(arguments) + " -Verb RunAs -WindowStyle Hidden -Wait -PassThru; if($p.ExitCode -ne 0){exit $p.ExitCode}"
	startedAt := time.Now().Unix()
	_, err = powershell(script)
	if err != nil {
		if detail := recentSetupFailure(startedAt); detail != "" {
			return output{"ok": false, "message": detail, "requiresElevation": true}
		}
		return output{"ok": false, "requiresElevation": true, "message": "لم تكتمل موافقة ويندوز أو فشلت التهيئة؛ افحص الاتصال قبل الامتحان."}
	}
	response := networkInspection("status")
	expected := request.Action != "restore"
	response["ok"] = response["configured"] == expected
	if !expected && response["ok"] == true {
		response["message"] = "تمت استعادة إعداد كابل الامتحان السابق."
	}
	return response
}
func recentSetupFailure(startedAt int64) string {
	content, err := os.ReadFile(filepath.Join(os.Getenv("ProgramData"), "Massar", "ExamNetwork", "last-setup-result.json"))
	if err != nil || len(content) > 8192 {
		return ""
	}
	var report struct {
		At     int64 `json:"at"`
		Result struct {
			OK      bool   `json:"ok"`
			Message string `json:"message"`
		} `json:"result"`
	}
	if json.Unmarshal(bytes.TrimPrefix(content, []byte{0xef, 0xbb, 0xbf}), &report) != nil || report.At < startedAt || report.Result.OK {
		return ""
	}
	return report.Result.Message
}

func applyElevated() {
	if len(os.Args) != 3 || len(os.Args[2]) > 8192 {
		os.Exit(2)
	}
	encoded, err := base64.RawURLEncoding.DecodeString(os.Args[2])
	if err != nil {
		os.Exit(2)
	}
	var request input
	if json.Unmarshal(encoded, &request) != nil {
		os.Exit(2)
	}
	if request.Action != "configure" && request.Action != "restore" && request.Action != "portal-configure" {
		os.Exit(2)
	}
	if request.Action != "restore" && !adapterGUID.MatchString(request.AdapterID) {
		os.Exit(2)
	}
	if request.Mode != "" && request.Mode != "legacy" && request.Mode != "https" {
		os.Exit(2)
	}
	if request.MaintenanceDeadline < time.Now().Add(120*time.Second).Unix() {
		os.Exit(2)
	}
	response, err := invokeScriptRequest(request)
	if err != nil || response["ok"] == false {
		os.Exit(1)
	}
}

func probeDNS() bool {
	connection, err := net.DialTimeout("udp4", examIP+":53", time.Second)
	if err != nil {
		return false
	}
	defer connection.Close()
	connection.SetDeadline(time.Now().Add(time.Second))
	query := []byte{0x6d, 0x61, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 4, 't', 'e', 's', 't', 0, 0, 1, 0, 1}
	if _, err = connection.Write(query); err != nil {
		return false
	}
	response := make([]byte, 512)
	n, err := connection.Read(response)
	return err == nil && n > 16 && bytes.Equal(response[:2], query[:2]) && bytes.Equal(response[n-4:n], serverIPv4)
}
func probePortal() bool {
	connection, err := net.DialTimeout("tcp4", examIP+":80", time.Second)
	if err != nil {
		return false
	}
	defer connection.Close()
	connection.SetDeadline(time.Now().Add(time.Second))
	if _, err = io.WriteString(connection, "GET /generate_204 HTTP/1.0\r\nHost: connectivitycheck.gstatic.com\r\n\r\n"); err != nil {
		return false
	}
	response := make([]byte, 1024)
	n, err := connection.Read(response)
	return err == nil && bytes.Contains(response[:n], []byte("302")) && bytes.Contains(response[:n], []byte("Location: http://10.77.0.1/"))
}

func probeStudentService() bool {
	client := &http.Client{Timeout: time.Second, Transport: &http.Transport{Proxy: nil}, CheckRedirect: func(*http.Request, []*http.Request) error { return http.ErrUseLastResponse }}
	response, err := client.Get("http://" + examIP + "/api/lounge")
	if err != nil {
		return false
	}
	defer response.Body.Close()
	if response.StatusCode != 200 {
		return false
	}
	var lounge map[string]any
	if json.NewDecoder(io.LimitReader(response.Body, 262144)).Decode(&lounge) != nil {
		return false
	}
	_, rooms := lounge["rooms"].([]any)
	_, serverTime := lounge["serverTime"].(float64)
	return rooms && serverTime
}

func enablePacketInfo(socket *net.UDPConn, index int) error {
	raw, err := socket.SyscallConn()
	if err != nil {
		return err
	}
	var settingErr error
	err = raw.Control(func(handle uintptr) {
		for _, option := range [][3]int{{windows.IPPROTO_IP, windows.IP_PKTINFO, 1}, {windows.SOL_SOCKET, windows.SO_BROADCAST, 1}, {windows.IPPROTO_IP, 31, int(binary.BigEndian.Uint32([]byte{byte(index), byte(index >> 8), byte(index >> 16), byte(index >> 24)}))}} {
			if settingErr = windows.SetsockoptInt(windows.Handle(handle), option[0], option[1], option[2]); settingErr != nil {
				return
			}
		}
	})
	if err != nil {
		return err
	}
	return settingErr
}
func packetInterface(control []byte) int {
	headerSize := int(unsafe.Sizeof(windows.WSACMSGHDR{}))
	alignment := int(unsafe.Sizeof(uintptr(0)))
	for len(control) >= headerSize {
		header := (*windows.WSACMSGHDR)(unsafe.Pointer(&control[0]))
		length := int(header.Len)
		if length < headerSize || length > len(control) {
			return 0
		}
		if header.Level == windows.IPPROTO_IP && header.Type == windows.IP_PKTINFO && length >= headerSize+int(unsafe.Sizeof(windows.IN_PKTINFO{})) {
			packet := (*windows.IN_PKTINFO)(unsafe.Pointer(&control[headerSize]))
			return int(packet.Ifindex)
		}
		advance := (length + alignment - 1) &^ (alignment - 1)
		if advance > len(control) {
			break
		}
		control = control[advance:]
	}
	return 0
}
