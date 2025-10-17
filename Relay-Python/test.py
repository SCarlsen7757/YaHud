import os
import socket
from multiprocessing import shared_memory
import signal
import time

SHARE_MEMORY_AREA_BYTE_SIZE = 43996


running = True

def handler(signum, frame):
    global running
    running = False
    
signal.signal(signal.SIGINT, handler)
signal.signal(signal.SIGTERM, handler)

def main():
    memory_file = shared_memory.SharedMemory("$R3E", True, SHARE_MEMORY_AREA_BYTE_SIZE)
    print("Running Mem test...")
    
    cnt = 1
    while running:
        memory_file.buf[0] = cnt
        cnt = (cnt + 1) % 20000
        
        time.sleep(0.1)
        
        
    print("Stopping Mem test...")
    # memory_file.close()

def cls():
    os.system('cls' if os.name=='nt' else 'clear')

if __name__ == "__main__":
    main()