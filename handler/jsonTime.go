package handler

import (
	"fmt"
	"time"
)

/*
JsonTime is simply a wrapper around a time.Time that enforces a standard ISO 8601 date format
which is the same as most javascript versions

toISOString() // Javascript
2006-01-02T15:04:05.000Z // Go
yyyy-MM-ddTHH:mm:ss.SSSZ // Java

yyyy : 4 digit year
MM   : 2 digit month (01 - 12)
dd   : 2 digit day of month (01 - 31)
T    : the letter T
HH   : 2 digit hour in 24 hour format (00 - 23)
mm   : 2 digit minutes (00 - 59)
ss   : 2 digit seconds (00 - 59)
SSS  : 3 digit milliseconds (000 - 999)
Z    : the letter Z which represents UTC or Zulu time.
*/

const dateFormat = "2006-01-02T15:04:05.000Z" // ISO 8601

type JsonTime time.Time

func (t JsonTime) MarshalJSON() ([]byte, error) {
	ts := fmt.Sprintf("\"%s\"", time.Time(t).Format(dateFormat))
	return []byte(ts), nil
}

func (t *JsonTime) UnmarshalJSON(b []byte) error {
	// Remove the leading and trailing double quotes...
	b = b[1 : len(b)-1]

	dt, err := time.Parse(dateFormat, string(b))
	if err != nil {
		return err
	}
	*t = JsonTime(dt)
	return nil
}

func (t JsonTime) String() string {
	return time.Time(t).Format(dateFormat)
}
