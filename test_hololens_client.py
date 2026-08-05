#!/usr/bin/env python3
import socket
import json
import argparse
import time
import sys

def scan_qr_code():
    # macOS-specific fix: pyzbar often fails to find libzbar.dylib on macOS
    # even if installed via Homebrew. We manually load it via ctypes first.
    import platform
    if platform.system() == 'Darwin':
        import ctypes
        import glob
        paths = [
            '/opt/homebrew/lib/libzbar.dylib',
            '/usr/local/lib/libzbar.dylib',
        ]
        paths.extend(glob.glob('/opt/homebrew/Cellar/zbar/*/lib/libzbar.dylib'))
        paths.extend(glob.glob('/usr/local/Cellar/zbar/*/lib/libzbar.dylib'))
        
        for path in paths:
            try:
                ctypes.CDLL(path)
                break
            except OSError:
                continue

    try:
        import cv2
        from pyzbar.pyzbar import decode
    except Exception as e:
        print(f"Error: Missing required libraries for QR scanning. (Details: {e})")
        print("Please install them using:")
        print("  pip3 install opencv-python pyzbar")
        print("Note: On macOS, you may also need to install the zbar library via Homebrew:")
        print("  brew install zbar")
        sys.exit(1)

    print("Opening webcam... Please hold the smartphone QR code up to the camera.")
    cap = cv2.VideoCapture(0)
    
    if not cap.isOpened():
        print("Error: Could not open webcam.")
        sys.exit(1)

    qr_data = None
    while True:
        ret, frame = cap.read()
        if not ret:
            print("Failed to grab frame.")
            break
            
        decoded_objects = decode(frame)
        for obj in decoded_objects:
            qr_data = obj.data.decode('utf-8')
            break
            
        if qr_data:
            break
            
        cv2.imshow("QR Scanner (Press 'q' to quit)", frame)
        if cv2.waitKey(1) & 0xFF == ord('q'):
            break

    cap.release()
    cv2.destroyAllWindows()
    
    return qr_data

def parse_qr_data(qr_data):
    # Expected format: "192.168.x.x:5000,ID:XYZ123ABC"
    try:
        parts = qr_data.split(',')
        ip_port = parts[0].split(':')
        ip = ip_port[0]
        port = int(ip_port[1])
        device_id = parts[1].split(':')[1]
        return ip, port, device_id
    except Exception as e:
        print(f"Error parsing QR data. Is it the correct QR code? Data: {qr_data}")
        sys.exit(1)

def main():
    parser = argparse.ArgumentParser(description='Simulate HoloLens connecting to the Smartphone AR app.')
    parser.add_argument('--ip', type=str, help='IP Address of the smartphone (from QR code)')
    parser.add_argument('--port', type=int, default=5000, help='Port of the smartphone (default 5000)')
    parser.add_argument('--id', type=str, help='Device ID (from QR code)')
    parser.add_argument('--loop', action='store_true', help='Continuously poll for location data')
    parser.add_argument('--interval', type=float, default=1.0, help='Polling interval in seconds (if --loop is used)')
    parser.add_argument('-qr', '--qr', action='store_true', help='Use MacBook webcam to scan the QR code')
    
    args = parser.parse_args()

    if args.qr:
        qr_data = scan_qr_code()
        if not qr_data:
            print("QR scan cancelled or failed.")
            sys.exit(1)
        print(f"Scanned QR Code: {qr_data}")
        ip, port, device_id = parse_qr_data(qr_data)
        args.ip = ip
        args.port = port
        args.id = device_id
    elif not args.ip or not args.id:
        print("Error: You must provide either '-qr' OR both '--ip' and '--id'")
        parser.print_help()
        sys.exit(1)

    print(f"Connecting to {args.ip}:{args.port} with Device ID: {args.id}...")

    def fetch_data():
        try:
            with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
                s.settimeout(5.0)
                s.connect((args.ip, args.port))
                
                request_message = f"GET_LOCATION ID:{args.id}\n"
                s.sendall(request_message.encode('ascii'))
                
                response_data = b""
                while True:
                    chunk = s.recv(4096)
                    if not chunk:
                        break
                    response_data += chunk
                
                response_str = response_data.decode('utf-8')
                
                try:
                    parsed_json = json.loads(response_str)
                    print("\n--- Received Geospatial Data ---")
                    print(json.dumps(parsed_json, indent=4))
                    print("--------------------------------")
                except json.JSONDecodeError:
                    print(f"\n[Raw Response]: {response_str}")

        except ConnectionRefusedError:
            print(f"Connection refused. Make sure the smartphone app is running and on the same Wi-Fi network ({args.ip}:{args.port}).")
        except socket.timeout:
            print("Connection timed out.")
        except Exception as e:
            print(f"An error occurred: {e}")

    if args.loop:
        print(f"Polling every {args.interval} seconds. Press Ctrl+C to stop.")
        try:
            while True:
                fetch_data()
                time.sleep(args.interval)
        except KeyboardInterrupt:
            print("\nStopped polling.")
    else:
        fetch_data()

if __name__ == "__main__":
    main()
