package main

import (
	"fmt"
	"net"
	"net/http"
	"os"
	"time"
)

func main() {
	client := http.Client{Timeout: 2 * time.Second}
	conn, err := net.DialTimeout("tcp", "127.0.0.1:5300", 2*time.Second)
	if err != nil {
		os.Exit(1)
	}
	conn.Close()

	response, err := client.Get("http://127.0.0.1:5300/health")
	if err != nil || response.StatusCode < 200 || response.StatusCode >= 300 {
		os.Exit(1)
	}
	response.Body.Close()
	fmt.Print("healthy")
}
