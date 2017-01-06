## 0.2.2 - 2017-01-06
- Remove pin on Rx dependency. Update to Reactive Extensions 3.x

## 0.2.1 - 2017-01-05
- performance improvements including a breaking change to emit lines in batches

## 0.1.10 - 2016-08-24
- Fix the observable serial line client

## 0.1.9 - 2016-08-23
- Fix an argument transposition on TCPIP connection constructors

## 0.1.8 - 2016-08-22
- Replace serial port accessor which was removed by mistake
- Make configuration parameter a requirement (as inheritance complicates optional constructor arguments)

## 0.1.7 - 2016-08-22
- Change to provide IO and parsing function by inheritance
- Replies produced by the parsing function can now be of any type

## 0.1.6 - 2016-08-19
- Downgrade Rx 3.x dependency to 2.2.5 repair compatibility with FSharp.Control.Reactive until it is updated

## 0.1.5 - 2016-08-15
- Handle a prompt without a newline as a response delimiter

## 0.1.4 - 2016-08-11
- Add support for command responses terminated with a delimiter such as a prompt

## 0.1.3 - 2016-08-03
- Separate opening the serial socket from starting the read loop to allow initialisation

## 0.1.2 - 2016-08-03
- Bumping release number again for clean re-release after tooling problem

## 0.1.1 - 2016-08-03
- Bumping release number for clean re-release

## 0.1.0 - 2016-08-03
- Initial open-source release
