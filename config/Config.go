package config

import (
	"io/ioutil"
	"log"
	"os"
	"path/filepath"
	"strconv"
	"strings"
	"sync"
)

const (
	filename = "settings.conf"
)

// Configuration comment
type Configuration struct {
	confs map[string]string
	mu    sync.RWMutex
}

var (
	c    *Configuration
	once sync.Once
)

// Config comment
func Config() *Configuration {
	once.Do(func() {
		c = new(Configuration)

		// Set the context by checking the environment variable SETTINGS_CONTEXT

		f, _ := filepath.Abs(filename)
		bytes, err := ioutil.ReadFile(f)

		for err != nil && f != "/"+filename {

			dir := filepath.Dir(f)
			dir = filepath.Join(dir, "..")

			f, _ = filepath.Abs(filepath.Join(dir, filename))
			bytes, err = ioutil.ReadFile(f)
		}

		if err != nil {
			log.Printf("Failed to read config ['%s'] - %s\n", f, err)
			os.Exit(1)
		}

		str := string(bytes)
		lines := strings.Split(str, "\n")

		c.confs = make(map[string]string, 0)

		for _, line := range lines {
			if len(line) > 0 {
				line = strings.Split(line, "#")[0]
				pos := strings.Index(line, "=")
				if pos != -1 {
					key := strings.TrimSpace(line[:pos])
					value := line[pos+1:]
					value = strings.TrimSpace(value)

					// As an edge case, remove the first and last characters
					// if they are both double quotes
					if len(value) > 2 && value[0] == '"' && value[len(value)-1] == '"' {
						value = value[1 : len(value)-1]
					}

					c.confs[key] = value
				}
			}
		}
	})

	return c
}

// Get (key, defaultValue)
func (c *Configuration) Get(key string, defaultValue ...string) (string, bool) {
	env := os.Getenv(key)
	if env != "" {
		return env, true
	}

	c.mu.RLock()
	defer c.mu.RUnlock()

	var (
		ret string
		ok  bool
	)

	// Start with a copy of the context, i.e. "live.eupriv"
	k := key
	for !ok {
		ret, ok = c.confs[k]
		if ok {
			break
		} else {
			pos := strings.LastIndex(k, ".")
			if pos == -1 {
				break
			}
			k = k[:pos]
		}
	}

	if ok {
		return ret, ok
	}

	if len(defaultValue) > 0 {
		ret = defaultValue[0]
	}

	return ret, false
}

// GetInt comment
func (c *Configuration) GetInt(key string, defaultValue ...int) (int, bool) {
	str, ok := c.Get(key)
	if str == "" || !ok {
		if len(defaultValue) > 0 {
			return defaultValue[0], false
		}
		return 0, false
	}

	i, err := strconv.Atoi(str)
	if err != nil {
		return 0, false
	}
	return i, ok
}

// GetBool comment
func (c *Configuration) GetBool(key string, defaultValue ...bool) bool {
	str, ok := c.Get(key)
	if str == "" || !ok {
		if len(defaultValue) > 0 {
			return defaultValue[0]
		}
		return false
	}

	i, err := strconv.ParseBool(str)
	if err != nil {
		return false
	}
	return i
}
