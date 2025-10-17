import datetime
import os
import socket
from multiprocessing import shared_memory
import signal
import time

SHARE_MEMORY_AREA_BYTE_SIZE = 43996
TIME_BETWEEN_UPDATES_SECONDS = 1 / 60

udp_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
running = True

update_times = []

def handler(signum, frame):
    global running
    running = False
    
signal.signal(signal.SIGINT, handler)
signal.signal(signal.SIGTERM, handler)

def main():
    memory_file = shared_memory.SharedMemory("$R3E")
    print("Running UDP Relay...")
    
    last_console_update = datetime.datetime.now()
    print(f"Average update time (ms): 0")
    timestamp = time.perf_counter_ns()
    while running:
        
        data = memory_file.buf.tobytes()
        send_udp(data)
        
        next_time = time.perf_counter_ns()
        
        update_times.append(next_time - timestamp)
        if len(update_times) > 20:
            update_times.pop(0)
            
        if datetime.datetime.now() - last_console_update > datetime.timedelta(seconds=1):
            cls()
            print("Running UDP Relay...")
            print(f"Average update time (ms): {(sum(update_times) / len(update_times)) / 1_000_000}")
            last_console_update = datetime.datetime.now()
        
        timestamp = next_time
        time.sleep(TIME_BETWEEN_UPDATES_SECONDS)
        
        
    print("Stopping UDP Relay...")
    memory_file.close()

def send_udp(data: bytes):
    udp_socket.sendto(data, ("127.0.0.1", 10101))

def cls():
    os.system('cls' if os.name=='nt' else 'clear')

if __name__ == "__main__":
    main()